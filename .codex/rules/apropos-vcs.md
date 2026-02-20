# Apropos VCS Command Mapping

Apropos generated this file for session startup.
Use these command mappings when operating on repository actions.

## Mappings

- `cd <target dir name>` => `ade useview <view name>`
- `git add .` => `ade mkelem <file name>`
- `git branch -a` => `ade lstrans -all`
- `git branch -D <trans_name>` => `ade endtrans; ade destroytrans or ade aborttrans -purge`
- `git branch -v` => `ade lstrans`
- `git checkout -- <file name>` => `ade unbranch <file name>`
- `git checkout -b <branch name>` => `ade begintrans <transaction name>`
- `git checkout -b <new_branch_name> <commit_hash>` => `ade grabtrans <transaction_name> -savepoint <savepoint_name>`
- `git checkout <trans_name>` => `ade begintrans -reopen <trans_name>`
- `git checkout master` => `ade endtrans`
- `git checkout master && git pull && git checkout <trans_name> && git merge master` => `ade refreshview -latest`
- `git checkout master && git pull && git merge <trans_name> && git push` => `ade beginmerge; ade mergetrans; ade endmerge`
- `git clone <git://url.git> <target dir name>` => `ade createview -label <view name>`
- `git commit -a -m "savepoint"` => `ade savepoint -c "<message>"`
- `git commit -am "Message"` => `ade ci -all`
- `git diff --cached --name-status` => `view staged changes relative to HEAD`
- `git diff --cached origin/master --name-status` => `ade describetrans`
- `git diff origin/master..HEAD` => `ade diff -label <file name> -gui`
- `git diff origin/master..HEAD --name-only` => `ade describetrans -short`
- `git log` => `ade savepoint -list`
- `git log origin/master..HEAD` => `ade lstrans -repos -since <YYYYMMDD>`
- `git merge origin/master; git push` => `ade beginmerge; ade mergetrans; ade endmerge`
- `git pull` => `ade refreshview -latest`
- `git push origin --delete <trans_name>` => `<not allowed in ADE>`
- `git push origin <trans_name>` => `ade savetrans`
- `git rebase master` => `ade refreshview -latest`
- `git reset --hard <commit_hash>` => `ade savepoint -revert <savepoint_name>`
- `git reset HEAD <file name> && rm <file name>` => `ade unmkelem <file name>`
- `git status` => `ade lsprivate`
- `no equivalent, automatic checkouts` => `ade co -nc <file name>`

## Plugin Workflow Rules

- Environment: ADE is authoritative for source control workflows in this project when ADE provider is detected.
- Do not run bulk checkout by default. Checkout files individually before editing: ade co -nc <file>.
- For transaction file summaries with unified diffs, prefer: ade describetrans -short -diff_txn_changes -unified_diff_format -diffs_only.
- For per-file unified diff against label, prefer: ade diff -label -unified_diff_format <file>.
- Use ADE transaction operations (begintrans/ci/unbranch/unmkelem/beginmerge/mergetrans/endmerge) instead of git branch/commit/merge semantics.
- Do not assume git worktrees in ADE mode; use ADE view selection (default path, existing view, or create view).

## Mapping Sources

- Global: `/Users/joshuabr/.apropos/plugins/vcs-mappings.json`
- Plugin: `/Users/joshuabr/.apropos/plugins/ade/vcs-mappings.json`
- Project: `/Users/joshuabr/code/Lean/.apropos/vcs-mappings.json`

