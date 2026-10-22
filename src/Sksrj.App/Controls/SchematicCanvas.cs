// SKSRJ Timetable generator
// Copyright (C) 2026 Oskar Jagodziński
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version, together with the additional terms
// under Section 7 of that License (see the files LICENSE and ADDITIONAL_TERMS).
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
// Based on SKSRJ Timetable generator originally created by Oskar Jagodziński.
// Original source: https://github.com/Hirek193/sksrj

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Sksrj.App.ViewModels;
using Sksrj.Core.Engine;
using Sksrj.Core.Layout;
using Sksrj.Core.Model;

namespace Sksrj.App.Controls;

/// <summary>Interaktywne płótno planu schematycznego scenerii.</summary>
public sealed class SchematicCanvas : Control
{
    private MainViewModel? _vm;
    private double _zoom = 0.75;
    private Vector _offset = new(30, 40);
    private PageDrawing? _cache;
    private readonly OpsRenderer _r = new();

    private enum DragMode { None, Pan, Node, Via }
    private DragMode _drag;
    private Point _last;
    private SceneryNode? _dragNode;
    private ViaPoint? _dragVia;
    private Vector _grab;
    private SceneryNode? _connectFrom;
    private SceneryNode? _hover;
    private Point _mouseWorld;

    /// <summary>Pędzle płótna dla motywu jasnego i ciemnego.</summary>
    private sealed class CanvasTheme
    {
        public readonly SchemeColors Colors;
        public readonly IBrush Paper, Dot, PrimaryBrush, OnPrimary;
        public readonly IPen RoutePen, SelEdgePen, SelPen, HoverPen, RubberPen, HandlePen;

        public CanvasTheme(SchemeColors c, Color primary, byte routeAlpha)
        {
            Colors = c;
            static IBrush B(Color x) => new SolidColorBrush(x).ToImmutable();
            Paper = B(Color.FromRgb(c.Background.R, c.Background.G, c.Background.B));
            Dot = B(Color.FromRgb(c.Dots.R, c.Dots.G, c.Dots.B));
            PrimaryBrush = B(primary);
            OnPrimary = c.Dark ? B(Color.FromRgb(0, 51, 83)) : Brushes.White;
            RoutePen = new Pen(B(Color.FromArgb(routeAlpha, primary.R, primary.G, primary.B)), 18, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            SelEdgePen = new Pen(B(Color.FromArgb(c.Dark ? (byte)110 : (byte)90, 255, 160, 0)), 16, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            SelPen = new Pen(PrimaryBrush, 3);
            HoverPen = new Pen(B(Color.FromArgb(140, primary.R, primary.G, primary.B)), 2);
            RubberPen = new Pen(PrimaryBrush, 2, new DashStyle(new double[] { 4, 3 }, 0));
            HandlePen = new Pen(PrimaryBrush, 2);
        }
    }

    private static readonly CanvasTheme LightTheme = new(SchemeColors.Light, Color.FromRgb(0, 99, 155), 70);
    private static readonly CanvasTheme DarkThemeB = new(SchemeColors.DarkTheme, Color.FromRgb(150, 204, 255), 85);
    private CanvasTheme Th => _vm?.SchemeDark == true ? DarkThemeB : LightTheme;
    private static readonly Typeface BadgeFace = new(new FontFamily("avares://SKSRJ/Assets/Fonts#Liberation Sans"), FontStyle.Normal, FontWeight.Bold);

    public SchematicCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public MainViewModel? Vm
    {
        get => _vm;
        set
        {
            if (_vm != null) { _vm.SchemeInvalidated -= OnSchemeChanged; _vm.FitSchemeRequested -= FitToView; }
            _vm = value;
            if (_vm != null) { _vm.SchemeInvalidated += OnSchemeChanged; _vm.FitSchemeRequested += FitToView; }
            OnSchemeChanged();
        }
    }

    private Scenery? Sc => _vm?.Project.Scenery;

    private void OnSchemeChanged()
    {
        _cache = null;
        if (_vm?.Tool != SchemeTool.Connect) _connectFrom = null;
        InvalidateVisual();
    }

    public void FitToView()
    {
        var sc = Sc;
        if (sc == null || Bounds.Width < 10) return;
        var (x0, y0, x1, y1) = SchematicRenderer.Bounds(sc);
        var z = Math.Min(Bounds.Width / (x1 - x0), Bounds.Height / (y1 - y0));
        _zoom = Math.Clamp(z, 0.15, 4);
        _offset = new Vector((Bounds.Width - (x1 - x0) * _zoom) / 2 - x0 * _zoom, (Bounds.Height - (y1 - y0) * _zoom) / 2 - y0 * _zoom);
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.PreviousSize.Width < 10 && e.NewSize.Width > 10) FitToView();
    }

    private Point ToWorld(Point p) => new((p.X - _offset.X) / _zoom, (p.Y - _offset.Y) / _zoom);

    private Point Snap(Point w, bool free)
    {
        var g = Sc?.GridSize ?? 13;
        if (free || g < 1) return w;
        return new Point(Math.Round(w.X / g) * g, Math.Round(w.Y / g) * g);
    }

    // ---------------------------------------------------------------- trafienia
    private SceneryNode? HitNode(Point w)
    {
        var sc = Sc; if (sc == null) return null;
        var r = Math.Max(11, 9 / _zoom);
        SceneryNode? best = null; var bd = double.MaxValue;
        foreach (var n in sc.Nodes)
        {
            var d = Math.Sqrt((n.X - w.X) * (n.X - w.X) + (n.Y - w.Y) * (n.Y - w.Y));
            if (d < r && d < bd) { bd = d; best = n; }
        }
        return best;
    }

    private SceneryEdge? HitEdge(Point w)
    {
        var sc = Sc; if (sc == null) return null;
        var tol = Math.Max(6, 7 / _zoom);
        SceneryEdge? best = null; var bd = double.MaxValue;
        foreach (var e in sc.Edges)
        {
            var d = SchematicGeometry.DistanceToPolyline(new Pt(w.X, w.Y), SchematicGeometry.EdgePath(sc, e));
            if (d < tol && d < bd) { bd = d; best = e; }
        }
        return best;
    }

    private ViaPoint? HitVia(Point w)
    {
        var e = _vm?.SelectedEdge; if (e == null) return null;
        var r = Math.Max(7, 7 / _zoom);
        return e.Via.FirstOrDefault(v => Math.Abs(v.X - w.X) < r && Math.Abs(v.Y - w.Y) < r);
    }

    // ---------------------------------------------------------------- rysowanie
    public override void Render(DrawingContext ctx)
    {
        var b = new Rect(Bounds.Size);
        var th = Th;
        ctx.FillRectangle(th.Paper, b);
        var sc = Sc;
        if (sc == null || _vm == null) return;

        // siatka kropek
        var g = sc.GridSize * 4;
        if (g * _zoom > 10)
        {
            var w0 = ToWorld(new Point(0, 0)); var w1 = ToWorld(new Point(b.Width, b.Height));
            for (var x = Math.Floor(w0.X / g) * g; x <= w1.X; x += g)
                for (var y = Math.Floor(w0.Y / g) * g; y <= w1.Y; y += g)
                    ctx.FillRectangle(th.Dot, new Rect(x * _zoom + _offset.X - 1, y * _zoom + _offset.Y - 1, 2, 2));
        }

        if (_cache == null)
        {
            _cache = new PageDrawing();
            SchematicRenderer.Render(new Sksrj.Core.Layout.Canvas(_cache, _vm.Measurer), sc, th.Colors);
        }

        using (ctx.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_offset.X, _offset.Y)))
        {
            // trasa pociągu
            var route = _vm.CurrentRoute;
            if (route != null)
                foreach (var hop in route.Hops) ctx.DrawGeometry(null, th.RoutePen, Geo(SchematicGeometry.EdgePath(sc, hop.Edge)));
            if (_vm.SelectedEdge is { } se) ctx.DrawGeometry(null, th.SelEdgePen, Geo(SchematicGeometry.EdgePath(sc, se)));

            _r.Draw(ctx, _cache.Ops);

            // uchwyty załamań
            if (_vm.SelectedEdge is { } se2)
                foreach (var v in se2.Via) ctx.DrawRectangle(th.Paper, th.HandlePen, new Rect(v.X - 5, v.Y - 5, 10, 10));
            if (_hover != null && _hover != _vm.SelectedNode) ctx.DrawEllipse(null, th.HoverPen, new Point(_hover.X, _hover.Y), 14, 14);
            if (_vm.SelectedNode is { } sn) ctx.DrawEllipse(null, th.SelPen, new Point(sn.X, sn.Y), 15, 15);

            // punkty trasy
            if (_vm.SelectedTimetable != null)
            {
                var i = 1;
                foreach (var id in _vm.SelectedTimetable.Route.Waypoints)
                {
                    var n = sc.Node(id);
                    if (n == null) continue;
                    var c = new Point(n.X + 13, n.Y - 13);
                    ctx.DrawEllipse(th.PrimaryBrush, new Pen(th.Paper, 2), c, 9, 9);
                    var ft = new FormattedText(i.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeFace, 10, th.OnPrimary);
                    ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
                    i++;
                }
            }

            if (_connectFrom != null && _vm.Tool == SchemeTool.Connect)
                ctx.DrawLine(th.RubberPen, new Point(_connectFrom.X, _connectFrom.Y), _mouseWorld);
        }
    }

    private static StreamGeometry Geo(IReadOnlyList<Pt> pts)
    {
        var g = new StreamGeometry();
        if (pts.Count < 2) return g;
        using var gc = g.Open();
        gc.BeginFigure(new Point(pts[0].X, pts[0].Y), false);
        for (var i = 1; i < pts.Count; i++) gc.LineTo(new Point(pts[i].X, pts[i].Y));
        gc.EndFigure(false);
        return g;
    }

    // ---------------------------------------------------------------- obsługa myszy i klawiatury
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm == null || Sc == null) return;
        Focus();
        var pt = e.GetCurrentPoint(this);
        var pos = pt.Position; var w = ToWorld(pos);
        var free = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _vm.Tool != SchemeTool.Route;
        _last = pos;

        if (pt.Properties.IsMiddleButtonPressed || (pt.Properties.IsRightButtonPressed && _vm.Tool != SchemeTool.Connect))
        {
            _drag = DragMode.Pan; e.Pointer.Capture(this); return;
        }
        if (pt.Properties.IsRightButtonPressed) { _connectFrom = null; InvalidateVisual(); return; }
        if (!pt.Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2 && _vm.Tool == SchemeTool.Select)
        {
            var via = HitVia(w);
            if (via != null && _vm.SelectedEdge != null) { _vm.SelectedEdge.Via.Remove(via); return; }
            var edge = HitEdge(w);
            if (edge != null && HitNode(w) == null) { var s = Snap(w, free); _vm.AddVia(edge, s.X, s.Y); return; }
        }

        switch (_vm.Tool)
        {
            case SchemeTool.Select:
            {
                var via = HitVia(w);
                if (via != null) { _drag = DragMode.Via; _dragVia = via; e.Pointer.Capture(this); return; }
                var node = HitNode(w);
                if (node != null)
                {
                    _vm.SelectNode(node);
                    _drag = DragMode.Node; _dragNode = node; _grab = new Vector(node.X - w.X, node.Y - w.Y);
                    e.Pointer.Capture(this); return;
                }
                var edge = HitEdge(w);
                if (edge != null) { _vm.SelectEdge(edge); return; }
                _vm.ClearSelection();
                _drag = DragMode.Pan; e.Pointer.Capture(this);
                return;
            }
            case SchemeTool.AddNode:
            {
                var node = HitNode(w);
                if (node != null) _vm.SelectNode(node);
                else { var s = Snap(w, free); _vm.AddNodeAt(s.X, s.Y); }
                return;
            }
            case SchemeTool.Connect:
            {
                var node = HitNode(w);
                if (node == null) { var s = Snap(w, free); node = _vm.AddNodeAt(s.X, s.Y); }
                if (_connectFrom != null && _connectFrom != node) _vm.Connect(_connectFrom, node);
                _connectFrom = node;
                _vm.SelectNode(node);
                InvalidateVisual();
                return;
            }
            case SchemeTool.Route:
            {
                var node = HitNode(w);
                if (node == null) return;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) _vm.RemoveWaypointNode(node);
                else _vm.AddWaypoint(node);
                return;
            }
            case SchemeTool.Delete:
            {
                var node = HitNode(w);
                if (node != null) { _vm.DeleteNode(node); return; }
                var edge = HitEdge(w);
                if (edge != null) _vm.DeleteEdge(edge);
                return;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var w = ToWorld(pos);
        _mouseWorld = w;
        var free = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (_drag)
        {
            case DragMode.Pan:
                _offset += pos - _last; _last = pos; InvalidateVisual(); return;
            case DragMode.Node when _dragNode != null:
            {
                var s = Snap(new Point(w.X + _grab.X, w.Y + _grab.Y), free);
                if (Math.Abs(_dragNode.X - s.X) > 0.01 || Math.Abs(_dragNode.Y - s.Y) > 0.01) { _dragNode.X = s.X; _dragNode.Y = s.Y; }
                return;
            }
            case DragMode.Via when _dragVia != null:
            {
                var s = Snap(w, free);
                _dragVia.X = s.X; _dragVia.Y = s.Y;
                return;
            }
        }
        var h = HitNode(w);
        if (h != _hover) { _hover = h; InvalidateVisual(); }
        else if (_connectFrom != null) InvalidateVisual();
        Cursor = _vm?.Tool switch
        {
            SchemeTool.AddNode or SchemeTool.Connect => new Cursor(StandardCursorType.Cross),
            SchemeTool.Delete => h != null || HitEdge(w) != null ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            _ => h != null ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
        };
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = DragMode.None; _dragNode = null; _dragVia = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetPosition(this);
        var w = ToWorld(pos);
        var nz = Math.Clamp(_zoom * Math.Pow(1.15, e.Delta.Y), 0.1, 6);
        _zoom = nz;
        _offset = new Vector(pos.X - w.X * nz, pos.Y - w.Y * nz);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm == null) return;
        switch (e.Key)
        {
            case Key.Delete: _vm.DeleteSelectionCommand.Execute(null); e.Handled = true; break;
            case Key.Escape: _connectFrom = null; _vm.ClearSelection(); InvalidateVisual(); e.Handled = true; break;
            case Key.F: FitToView(); e.Handled = true; break;
            case Key.V: _vm.Tool = SchemeTool.Select; break;
            case Key.N: _vm.Tool = SchemeTool.AddNode; break;
            case Key.C: _vm.Tool = SchemeTool.Connect; break;
            case Key.T: _vm.Tool = SchemeTool.Route; break;
        }
    }
}
