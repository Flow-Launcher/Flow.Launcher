{
  description = "Flow Launcher (Avalonia) development shell for Linux";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { nixpkgs, ... }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});
    in
    {
      devShells = forAllSystems (pkgs:
        let
          # Native libraries Avalonia's X11 backend, Skia and the .NET runtime dlopen at run time.
          runtimeLibs = with pkgs; [
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
            zlib
          ];
        in
        {
          default = pkgs.mkShell {
            packages = with pkgs; [
              dotnet-sdk_10
              jujutsu
              git
            ];
            LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
            DOTNET_ROOT = "${pkgs.dotnet-sdk_10}/share/dotnet";
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          };
        });
    };
}
