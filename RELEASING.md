# Releasing

This package publishes to NuGet.org as `Demografix`. Consumers install it with:

```sh
dotnet add package Demografix
```

Publishing runs from `.github/workflows/release.yml` when a tag matching `vX.Y.Z` is
pushed. The workflow packs the project, pushes the `.nupkg` to NuGet.org, and creates a
GitHub Release.

## One-time setup

You need a NuGet.org account that owns (or co-owns) the `Demografix` package ID. Reserve
the ID with a first manual `dotnet nuget push` if it does not exist yet, then configure
one of the two authentication methods below.

### Trusted Publishing via OIDC (preferred)

Trusted Publishing lets the workflow exchange a GitHub OIDC token for a short-lived NuGet
API key, so there is no long-lived secret to store or rotate.

1. Sign in to NuGet.org and open your account at **Trusted Publishing**
   (`https://www.nuget.org/account/trustedpublishing`).
2. Create a trusted publisher policy bound to:
   - Package owner: your NuGet account or organization that owns `Demografix`.
   - Repository owner: `DemografixGenderize`.
   - Repository: `demografix-dotnet`.
   - Workflow file: `release.yml`.
   - Environment: `release`.
3. Save the policy. Note the NuGet username the policy belongs to.
4. In the GitHub repository, add a repository **Environment** named `release`
   (Settings -> Environments -> New environment). The release job references it.
5. Add a repository secret `NUGET_USER` set to the NuGet username from step 3. The
   `NuGet/login@v1` action uses it to look up the trusted publisher policy.

The release job already sets `permissions: id-token: write`, which is required for the
OIDC token exchange.

### API key fallback

If Trusted Publishing is not configured, fall back to a stored API key.

1. Create an API key at `https://www.nuget.org/account/apikeys` scoped to push for the
   `Demografix` package.
2. Add it as a repository secret named `NUGET_API_KEY`.
3. Replace the OIDC steps in `release.yml` so the push uses the stored key:

   ```yaml
   - name: Push to NuGet.org
     run: >
       dotnet nuget push "artifacts/*.nupkg"
       --source https://api.nuget.org/v3/index.json
       --api-key "${{ secrets.NUGET_API_KEY }}"
       --skip-duplicate
   ```

   Remove the `NuGet/login@v1` step when using this path.

## Cutting a release

1. Bump `<Version>` in `src/Demografix/Demografix.csproj` to the new `X.Y.Z`.
2. Commit the bump:

   ```sh
   git add src/Demografix/Demografix.csproj
   git commit -m "Release vX.Y.Z"
   ```

3. Tag and push the tag:

   ```sh
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

The release workflow verifies that the tag version matches `<Version>` in the manifest
before it packs and publishes. If they disagree, the run fails and nothing is published.
