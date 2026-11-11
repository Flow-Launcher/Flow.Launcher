{
  description = "Flow Launcher (Avalonia) development shell and runtime libraries for Linux";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { nixpkgs, ... }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});

      # Native libraries Avalonia's Wayland and X11 backends, Skia and the .NET runtime dlopen at run time.
      runtimeLibs = pkgs: with pkgs; [
        fontconfig
        freetype
        icu
        libGL
        libICE
        libSM
        libX11
        libXcursor
        libXext
        libXi
        libXrandr
        libXtst
        libxcb
        libxkbcommon
        openssl
        wayland
        zlib
      ];
    in
    {
      # Everything a published build needs on NixOS in one lib/ folder: the libraries above plus the C/C++ runtime
      # and dynamic loader the self-contained .NET runtime links against (used by Scripts/linux/install-nixos.sh).
      packages = forAllSystems (pkgs: {
        runtime-libs = pkgs.symlinkJoin {
          name = "flow-launcher-runtime-libs";
          paths = map pkgs.lib.getLib (runtimeLibs pkgs ++ [ pkgs.stdenv.cc.cc pkgs.glibc ]);
        };
      });

      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = with pkgs; [
            dotnet-sdk_10
            jujutsu
            git
          ];
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath (runtimeLibs pkgs);
          DOTNET_ROOT = "${pkgs.dotnet-sdk_10}/share/dotnet";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        };
      });
    };
}
