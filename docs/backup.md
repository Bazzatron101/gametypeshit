# Backups

Version control is git with Git LFS for binaries (see `.gitattributes`).
Unity regenerates `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the IDE files,
so they are ignored.

## Remote

The remote is GitHub: `https://github.com/Bazzatron101/gametypeshit` (`origin`). **It is public**:
everything pushed there can be read by anyone (the developer chose to keep it public on 2026-11-14).
Push at the end of each session:

    git push origin main

LFS objects go up with the commits. To check that nothing is left behind, run
`git lfs push origin main --dry-run`; it should list nothing.

Never commit secrets (Steam keys, passwords, tokens). Saves live outside the project
(`%USERPROFILE%/AppData/LocalLow/...`), and test output (`TestResults/`) and builds (`Builds/`)
are ignored.

## Offline backup routine (weekly, and before any risky change)

A remote is not a backup if the account is lost. Once a week, after the last session
of the week, write a self-contained bundle to an external drive:

    # from the project root; replace E: with the external drive
    git bundle create E:/hunter-backups/hunter-$(date +%F).bundle --all
    git lfs fetch --all
    robocopy .git/lfs E:/hunter-backups/lfs /E

To restore on a fresh machine:

    git clone E:/hunter-backups/hunter-YYYY-MM-DD.bundle hunter
    robocopy E:/hunter-backups/lfs hunter/.git/lfs /E
    cd hunter && git lfs checkout

Keep the last 4 weekly bundles. Test a restore at least once per milestone.
