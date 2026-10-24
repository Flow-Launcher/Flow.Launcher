# Everything SDK Binary Provenance

The Explorer plugin redistributes voidtools SDK wrappers that communicate with a separately installed Everything client over inter-process communication (IPC). Flow Launcher does not bundle the Everything application.

## Packaged Binary Inventory

| Path | SHA-256 | Signer thumbprint |
|---|---|---|
| `x64\Everything.dll` | `ae856af0c30068d9ba4c65d64ec3b30fda85c62914141bcd79a6381074a84948` | `B5B6468C781744765A590C0FE13AA418FC3335D1` |
| `x64\Everything3.dll` | `be25b01c73bbf359b50ddf30255133225f93b4bc40a8d208173319373bcdaa5c` | `6C8A3919279E9756765978716EB07C8052F5D1DE` |
| `arm64\Everything.dll` | `8531ea393677dd8fd37bed7420ac93344cd458b9a1324ba65c4a75d024d61886` | `6C8A3919279E9756765978716EB07C8052F5D1DE` |
| `arm64\Everything3.dll` | `0ef26560d1c0224686e67134ada57171f40f326a872ee8a1f2200e973f49f871` | `6C8A3919279E9756765978716EB07C8052F5D1DE` |

Portable packaging enforces the active architecture's hashes, valid Authenticode status, and signer thumbprints before creating the ZIP.

## ARM64 SDK2 Wrapper

- Packaged name: `arm64\Everything.dll`
- Vendor artifact: `EverythingARM64.dll`
- Source archive: `https://www.voidtools.com/Everything-SDK.zip`
- Archive retrieved: September 15, 2026
- Archive `Last-Modified`: August 14, 2026
- Archive SHA-256: `f5716d9513cce6b462b5170a0a2e7e081e191d9bcac6774f5decc061497df443`
- Binary SHA-256: `8531ea393677dd8fd37bed7420ac93344cd458b9a1324ba65c4a75d024d61886`
- PE architecture: ARM64
- Authenticode: valid; signer `voidtools PTY LTD`
- API compatibility: 88 exports, identical to the existing x64 wrapper; all Flow Launcher imports are present.

## ARM64 SDK3 Wrapper

- Packaged name: `arm64\Everything3.dll`
- Vendor artifact: `Everything3_ARM64.dll`
- Source release: `https://github.com/voidtools/everything_sdk3/releases/tag/3.0.0.9`
- Release published: October 11, 2025
- Archive SHA-256: `124685d35a5f49f3c1e9898853e166215748c893782c6a251f5dde58dacad4fa`
- Binary version: `3.0.0.9`
- Binary SHA-256: `0ef26560d1c0224686e67134ada57171f40f326a872ee8a1f2200e973f49f871`
- PE architecture: ARM64
- Authenticode: valid; signer `voidtools PTY LTD`
- API compatibility: 171 exports, identical to the existing x64 wrapper; all Flow Launcher imports are present.

## Runtime Verification

Verification used the official signed `Everything 1.5.0.1423b` ARM64 portable client on an ARM64 Windows device. An ARM64 .NET process loaded both wrappers and each returned the exact indexed fixture path on its first query.

- Everything ARM64 application SHA-256: `187e510f1a943257a3e4a2ea7a77612b9dc47b0cda52bbaa90c5a41a1e93ac9d` (portable archive)
- SDK2 result: passed
- SDK3 result: passed

Windows Search remains available without Everything and is the default Explorer search provider.
