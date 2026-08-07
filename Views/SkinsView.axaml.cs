using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using GreenLauncher.Rendering;

namespace GreenLauncher.Views;

// 스킨 업로드/선택은 로컬 UI 상태만 다룹니다 — Mojang 스킨 업로드 API 연동은 아직 없습니다.
public partial class SkinsView : UserControl
{
    private static readonly IBrush SelectedBorder = Brush.Parse("#89D22F");
    private static readonly IBrush UnselectedBorder = Brushes.Transparent;
    private static readonly IBrush SelectedLabelBrush = Brush.Parse("#4F7A1A");
    private static readonly IBrush UnselectedLabelBrush = Brush.Parse("#6B7280");

    private readonly List<SkinItem> _skins = new()
    {
        new SkinItem("default", "기본 (Steve)", null),
    };

    private bool _has3D = true;
    private string _selectedId = "default";
    private Point? _lastPointer;

    public SkinsView()
    {
        InitializeComponent();

        SkinViewer3D.SetBackgroundColor(255, 255, 255);
        SkinViewer3D.SetBackgroundImage(SkinViewerControl.LoadBundledBackground("bg_sunset.png"));
        SkinViewer3D.InitFailed += (_, _) =>
        {
            // OpenGL 컨텍스트 생성/셰이더 컴파일 실패(일부 macOS/드라이버 환경) —
            // 3D 뷰를 트리에서 제거하고 기존 2D 폴백으로 전환.
            _has3D = false;
            if (SkinViewer3D.Parent is Panel host) host.Children.Remove(SkinViewer3D);
            SkinViewerInteractionOverlay.IsVisible = false;
            Refresh();
        };

        SkinViewerInteractionOverlay.PointerPressed += OnViewerPointerPressed;
        SkinViewerInteractionOverlay.PointerMoved += OnViewerPointerMoved;
        SkinViewerInteractionOverlay.PointerReleased += OnViewerPointerReleased;
        SkinViewerInteractionOverlay.PointerWheelChanged += OnViewerPointerWheelChanged;

        Refresh();
    }

    private void OnViewerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastPointer = e.GetPosition(SkinViewerInteractionOverlay);
        e.Pointer.Capture(SkinViewerInteractionOverlay);
    }

    private void OnViewerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPointer is not { } last || e.Pointer.Captured != SkinViewerInteractionOverlay) return;
        var pos = e.GetPosition(SkinViewerInteractionOverlay);
        SkinViewer3D.ApplyDrag(pos.X - last.X, pos.Y - last.Y);
        _lastPointer = pos;
    }

    private void OnViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _lastPointer = null;
        e.Pointer.Capture(null);
    }

    private void OnViewerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        SkinViewer3D.ApplyZoom(e.Delta.Y);
    }

    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "스킨 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("PNG 이미지") { Patterns = new[] { "*.png" } } }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        await using var stream = await file.OpenReadAsync();
        var bitmap = new Bitmap(stream);

        var id = "up-" + Guid.NewGuid().ToString("N")[..8];
        var name = Path.GetFileNameWithoutExtension(file.Name);
        _skins.Add(new SkinItem(id, name, bitmap));
        _selectedId = id;
        Refresh();
    }

    // 로그인 계정에서 실제로 불러온 스킨을 목록의 기본 항목에 반영한다.
    // MainWindow가 스킨 URL을 성공적으로 로드했을 때 호출.
    public void SetCurrentSkin(Bitmap skinBitmap, bool isSlim)
    {
        var updated = new SkinItem("default", "현재 스킨", skinBitmap) { IsSlim = isSlim };
        var index = _skins.FindIndex(s => s.Id == "default");
        if (index >= 0) _skins[index] = updated;
        else _skins.Insert(0, updated);
        Refresh();
    }

    private void OnSkinItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkinItem item }) return;
        _selectedId = item.Id;
        Refresh();
    }

    private void OnSlimArmToggleClick(object? sender, RoutedEventArgs e)
    {
        var index = _skins.FindIndex(s => s.Id == _selectedId);
        if (index < 0) return;
        _skins[index] = _skins[index] with { IsSlim = !_skins[index].IsSlim };
        Refresh();
    }

    private void Refresh()
    {
        var displayItems = _skins.Select(s => s with
        {
            BorderBrush = s.Id == _selectedId ? SelectedBorder : UnselectedBorder,
            StatusLabel = s.Id == _selectedId ? "적용중" : "적용하기",
            StatusForeground = s.Id == _selectedId ? SelectedLabelBrush : UnselectedLabelBrush,
            HasThumbnail = s.Thumbnail is not null
        }).ToList();

        SkinListItems.ItemsSource = displayItems;

        var selected = displayItems.First(s => s.Id == _selectedId);
        SkinPreviewName.Text = selected.Name;
        SlimArmToggleThumb.Margin = new Thickness(selected.IsSlim ? 21 : 3, 0, 0, 0);
        ApplyPreview(selected);
    }

    private void ApplyPreview(SkinItem selected)
    {
        // "기본 (Steve)"처럼 실제 텍스처가 없는 자리표시자는 3D로 보여줄 게 없으니
        // WebView 검토 때와 마찬가지로 2D 폴백을 그대로 쓴다.
        if (_has3D && selected.Thumbnail is not null)
        {
            SkinPreviewFace.IsVisible = false;
            SkinViewer3D.IsVisible = true;
            SkinViewerInteractionOverlay.IsVisible = true;
            SkinViewer3D.SetSkin(selected.Thumbnail, selected.IsSlim);
            return;
        }

        if (_has3D)
        {
            SkinViewer3D.IsVisible = false;
            SkinViewerInteractionOverlay.IsVisible = false;
        }
        SkinPreviewFace.IsVisible = true;

        if (selected.Thumbnail is not null)
        {
            SkinPreviewImage.Source = selected.Thumbnail;
            SkinPreviewImage.IsVisible = true;
            SkinPreviewPlaceholder.IsVisible = false;
        }
        else
        {
            SkinPreviewImage.IsVisible = false;
            SkinPreviewPlaceholder.IsVisible = true;
        }
    }
}

public record SkinItem(string Id, string Name, Bitmap? Thumbnail)
{
    public IBrush BorderBrush { get; init; } = Brushes.Transparent;
    public string StatusLabel { get; init; } = "";
    public IBrush StatusForeground { get; init; } = Brushes.Transparent;
    public bool HasThumbnail { get; init; }
    public bool IsSlim { get; init; } // 업로드한 스킨은 모델 타입을 몰라 기본 false(클래식)
}
