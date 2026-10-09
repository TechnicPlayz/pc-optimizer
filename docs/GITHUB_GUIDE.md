# Velora PC - GitHub guide (no coding tools needed)

GitHub does three jobs for you: **stores the code**, **builds the EXE + installer for free**, and **hosts the download your users (and the in-app updater) use**.

---

## Part 1 - Upload the project (once)

1. Go to your repo: `github.com/TechnicPlayz/pc-optimizer`.
2. **Delete the old files** so they don't clash: open each of `Program.cs`, `PCMaintenanceTool.csproj`, `app.manifest`, `README.txt`, `BUILD.bat`, click the **trash icon** (top right of the file), then **Commit changes**.
3. Unzip `VeloraPC-repo.zip` on your PC.
4. In the repo click **Add file > Upload files** and drag in **everything** from the unzipped folder (`src`, `installer`, `docs`, `.github`, `README.md`, `CHANGELOG.md`, `EULA.txt`, `PRIVACY.txt`, `BUILD.bat`, `.gitignore`). The installer script needs `EULA.txt` and `PRIVACY.txt`, and the app needs everything under `src`, so don't upload just the project file on its own.
   - Can't see `.github`? In File Explorer turn on **View > Show > Hidden items**.
   - If `.github` won't drag in: **Add file > Create new file**, type `.github/workflows/build.yml` as the name, paste the contents of that file.
5. Scroll down, click **Commit changes**.

## Part 2 - Get the EXE and installer

1. Click the **Actions** tab. A run called **Build Velora PC** starts by itself (about 3-5 minutes).
2. Green tick = success. Red cross = click it, open the red step, copy the error and send it to me.
3. Click the finished run, scroll to **Artifacts**, download **VeloraPC**. Unzip it:
   - `VeloraPC.exe` - the app itself (runs without installing)
   - `VeloraPC-Setup-1.0.0.exe` - the installer (adds Start menu entry + uninstaller)

## Part 3 - Publish a release (this is what users download)

1. On the repo home page click **Releases** (right side) > **Create a new release**.
2. Click **Choose a tag**, type `v1.0.0`, click **Create new tag**.
3. Title: `Velora PC 1.0.0`. In the description write what's new (the in-app updater shows this text!).
4. Make sure **Set as the latest release** is ticked and **pre-release / draft are NOT** ticked.
5. Click **Publish release**.
6. Wait ~5 minutes. Refresh the page: `VeloraPC.exe` and `VeloraPC-Setup-1.0.0.exe` appear under **Assets**. Send people that page link.

> If the files never appear: **Settings > Actions > General > Workflow permissions > Read and write permissions > Save**, then **Actions > the run > Re-run all jobs**.

## Part 4 - How updates reach your users

The app asks GitHub for the **latest release** when it starts (and on **Settings > Updates > Check for updates**).
If the release number is higher than the installed one, users see an **Update available** banner on the Dashboard and a **Download & install** button in Settings. The app downloads the installer, checks its SHA-256 fingerprint, installs, and reopens itself.

To ship an update:
1. Make your change (edit files on GitHub with the pencil icon, or upload new ones) and **Commit**.
2. Wait for the green tick in **Actions**.
3. **Releases > Create a new release**, new tag **higher than before** (`v1.0.1`, `v1.1.0`...), write what's new, **Publish**.
4. About 5 minutes later the installer is attached and every user's app will offer the update.

**Rules for updates to work**
- The repo must be **Public** (a private repo hides releases from the app).
- Tags look like `v1.2.3` (three numbers). The version in the app comes from the tag.
- The release must be the **latest**, not a draft or pre-release.
- The installer file must be named `VeloraPC-Setup-<version>.exe` (the workflow does this for you).

## Part 5 - Test the updater yourself

1. Publish release `v1.0.0` (Part 3), download and **install** `VeloraPC-Setup-1.0.0.exe`.
2. Make a tiny change, commit, wait for the green tick, publish release `v1.0.1`.
3. Open the installed Velora PC > **Settings > Updates > Check for updates**. You should see "Version 1.0.1 is available" and the install button.

## Troubleshooting

| Problem | Fix |
|---|---|
| Build is red | Open the failed step in **Actions**, copy the error, send it to me |
| No files under Assets | Fix workflow permissions (see Part 3 note), re-run the job |
| "No releases have been published yet" in the app | Publish a release (Part 3); repo must be public |
| Update says available but no install button | The installer asset isn't attached yet, wait a few minutes or check the file name |
| "Windows protected your PC" | Click **More info > Run anyway** (goes away only with a code-signing certificate) |
| Installed to a different place after updating | Not a problem: updates reuse the original install folder |
