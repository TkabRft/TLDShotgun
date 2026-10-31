# Local Git storage

Setup and verification: 2026-10-30.

These notes describe this checkout's local configuration. The GitHub fork's history was not rewritten, and other clones do not automatically inherit the configuration, hooks, or exclusions described here.

## Partial fetching

Both `origin` and `upstream` are configured with:

```ini
promisor = true
partialclonefilter = blob:none
```

All commit IDs, branches, and tags were preserved. Current file contents are cached locally. Older file contents are retrieved from GitHub when needed, so historical file inspection or checking out an old version can require an internet connection.

Normal fetch and merge commands still apply:

```powershell
git fetch upstream
git merge upstream/main
```

The cache can grow when fetching new versions, creating commits, or inspecting older files. The filter avoids downloading every historical file during fetch; it does not impose a fixed size limit or evict previously cached contents. Ordinary `git gc` cannot evict file contents that remain referenced by history.

## Generated-output guard

The local `.git/hooks/pre-commit` hook rejects added or modified:

- `*.modcomponent` and `*.bundle` files;
- outputs under `unity/AssetBundles/` and `DLLs/`;
- root `Shotgun.dll` and `DoubleBarrel.dll` outputs.

It permits source changes and output deletions. Local `.git/info/exclude` rules hide newly generated packages, bundles, and Unity caches. Ignore rules do not untrack existing files.

Existing tracked asset outputs remain available. In particular, `DoubleBarrel.modcomponent` is embedded by `Shotgun.csproj`; removing it requires a replacement asset-build or pinned-download workflow. Upstream merges can still update the tracked outputs needed by the project.

For a deliberately authorized build-artifact commit, PowerShell can set the hook's explicit override for one command sequence:

```powershell
$previousArtifactOverride = $env:TLD_ALLOW_BUILD_ARTIFACTS
try {
    $env:TLD_ALLOW_BUILD_ARTIFACTS = '1'
    git commit
} finally {
    if ($null -eq $previousArtifactOverride) {
        Remove-Item Env:TLD_ALLOW_BUILD_ARTIFACTS -ErrorAction SilentlyContinue
    } else {
        $env:TLD_ALLOW_BUILD_ARTIFACTS = $previousArtifactOverride
    }
}
```

Prefer GitHub release attachments for distributable builds when publication is requested. Do not bypass the guard as routine build cleanup.

## Recreating a compact checkout

If the cache grows substantially, first ensure local work is preserved, including local branches, uncommitted changes, and untracked files. After the necessary commits are pushed under the user's authorization, a new checkout can be created alongside the old one:

```powershell
git clone --filter=blob:none https://github.com/TkabRft/TLDShotgun.git TLDShotgun-compact
git -C TLDShotgun-compact remote add upstream https://github.com/monsieurmeh/Shotgun.git
git -C TLDShotgun-compact config remote.upstream.promisor true
git -C TLDShotgun-compact config remote.upstream.partialclonefilter blob:none
```

Run the clone command from the directory that should contain the new checkout. Verify the new copy and transfer the local hook, exclusion rules, and any required local-only files before retiring the old copy. These commands do not preserve unpushed work automatically.

## Verified result

| Check | Result |
|---|---|
| Git directory size | 509.23 MiB → 79.78 MiB |
| Space reclaimed | approximately 429 MiB |
| Whole project after cleanup | 288.16 MiB |
| Commits preserved | 241 |
| Tracked files checked unchanged | 258 |
| Branches and tags | unchanged |
| Integrity | `git fsck --full` passed |
| Network behavior | historical file retrieval and filtered upstream fetch passed |
| Local guard | source changes, blocked outputs, deletions, and explicit override checked with a temporary index |

Temporary copies were removed after verification. No remote push or history rewrite was performed.
