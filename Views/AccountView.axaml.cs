using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace GreenLauncher.Views;

public partial class AccountView : UserControl
{
    public event Action? LogoutRequested;

    public AccountView()
    {
        InitializeComponent();
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e) => LogoutRequested?.Invoke();

    public void SetProfile(string nickname, string uuid)
    {
        NicknameText.Text = nickname;
        UuidText.Text = uuid;
    }

    public void SetAvatar(IImage bitmap)
    {
        AvatarImage.Source = bitmap;
        AvatarImage.IsVisible = true;
        AvatarPlaceholder.IsVisible = false;
    }

    public void ResetAvatar()
    {
        AvatarImage.IsVisible = false;
        AvatarPlaceholder.IsVisible = true;
    }
}
