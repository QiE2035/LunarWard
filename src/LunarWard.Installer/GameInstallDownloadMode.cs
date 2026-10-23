using System;

namespace LunarWard.Installer;

[Flags]
public enum GameInstallDownloadMode
{

    Undefined = 0,

    SingleFile = 1,

    CompressedPackage = 2,

    Chunk = 4,

    Patch = 8,

}
