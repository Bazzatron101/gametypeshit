# Backups

Version control is git with Git LFS for binaries (see `.gitattributes`).
Unity regenerates `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the IDE files,
so they are ignored.

## Remote

No remote is configured yet. Once you've created an empty repository (GitHub or GitLab;
both support LFS), run:

    git remote add origin <url>
    git push -u origin main

LFS objects are pushed along with the commits.

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
