# DailyWords

A desktop word card app built with C#.

## Projects

- `DailyWords`: the original Windows WPF app
- `DailyWords.Mac`: the macOS app built with Avalonia

## Local run

```powershell
dotnet build DailyWords.csproj
dotnet build DailyWords.Mac\DailyWords.Mac.csproj
```

## macOS package

GitHub Actions builds `.dmg` files from `.github/workflows/build-macos-dmg.yml`.
