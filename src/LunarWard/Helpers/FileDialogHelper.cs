using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LunarWard.Helpers;

/// <summary>
/// 上游 <c>Starward.Helpers.FileDialogHelper</c>（Win32 COM IFileOpenDialog + WinRT StorageFile）的
/// Avalonia 等价实现：底层统一走 <see cref="IStorageProvider"/>（Linux 上由桌面门户/GTK 文件对话框提供）。
/// </summary>
/// <remarks>
/// 公开 API 与上游同名同参（<c>params (string Name, string Extension)[]</c> 过滤器形状保持一致），
/// 只把宿主参数从 <c>nint parentWindow</c> / <c>XamlRoot</c> 换成 <see cref="TopLevel"/>（以及便捷的
/// <see cref="Visual"/> 重载）。上游的 <c>FileDialogNative.cs</c>（386 行 COM 互操作）在 Linux 上没有对等物，
/// 不移植；相关差异见 docs/09-LINUX-DIVERGENCES.md。
/// </remarks>
public static class FileDialogHelper
{

    public static Task<string?> PickSingleFileAsync(Visual owner, params (string Name, string Extension)[] fileTypeFilter)
    {
        return PickSingleFileAsync(TopLevel.GetTopLevel(owner)!, fileTypeFilter);
    }


    public static async Task<string?> PickSingleFileAsync(TopLevel? topLevel, params (string Name, string Extension)[] fileTypeFilter)
    {
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return null;
        }
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = ToFileTypes(fileTypeFilter),
        }).ConfigureAwait(true);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }


    public static Task<List<string>> PickMultipleFilesAsync(Visual owner, params (string Name, string Extension)[] fileTypeFilter)
    {
        return PickMultipleFilesAsync(TopLevel.GetTopLevel(owner)!, fileTypeFilter);
    }


    public static async Task<List<string>> PickMultipleFilesAsync(TopLevel? topLevel, params (string Name, string Extension)[] fileTypeFilter)
    {
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return [];
        }
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = ToFileTypes(fileTypeFilter),
        }).ConfigureAwait(true);
        return files.Select(x => x.TryGetLocalPath()).Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();
    }


    public static Task<string?> OpenSaveFileDialogAsync(Visual owner, string? fileName = null, params (string Name, string Extension)[] fileTypeFilter)
    {
        return OpenSaveFileDialogAsync(TopLevel.GetTopLevel(owner)!, fileName, fileTypeFilter);
    }


    public static async Task<string?> OpenSaveFileDialogAsync(TopLevel? topLevel, string? fileName = null, params (string Name, string Extension)[] fileTypeFilter)
    {
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return null;
        }
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = fileName,
            FileTypeChoices = ToFileTypes(fileTypeFilter),
            DefaultExtension = fileTypeFilter.FirstOrDefault().Extension?.TrimStart('.'),
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }


    public static Task<string?> PickFolderAsync(Visual owner)
    {
        return PickFolderAsync(TopLevel.GetTopLevel(owner)!);
    }


    public static async Task<string?> PickFolderAsync(TopLevel? topLevel)
    {
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return null;
        }
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        }).ConfigureAwait(true);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }


    private static FilePickerFileType[] ToFileTypes((string Name, string Extension)[] fileTypeFilter)
    {
        if (fileTypeFilter.Length == 0)
        {
            return [FilePickerFileTypes.All];
        }
        return fileTypeFilter.Select(x => new FilePickerFileType(x.Name)
        {
            Patterns = [x.Extension.StartsWith('*') ? x.Extension : "*" + x.Extension],
        }).ToArray();
    }

}
