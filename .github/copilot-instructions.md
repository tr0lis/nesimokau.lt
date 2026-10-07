# Copilot Instructions

## Project Guidelines
- When requesting UI fixes, user wants only scoped changes and no unrelated redesign/regressions.
- User prefers compact, consistent UI: action buttons in diagnostics should use the same violet primary style as gameplay buttons, and Media Library 'Žiūrėti vėliau' must be right-aligned on the same row as subject filters.
- User prefers the hint action naming to be 'IŠSPRĘSTI' and wants the action disabled when coins are insufficient, with compact icon-only placement near the answer textbox.
- User prefers mobile menu popup to have a solid colored background (not transparent) and the mobile profile page must be fully responsive.
- Desktop subject cards should keep 2-word titles on one line with less compressed descriptions, and the 'Atidaryti' button should be replaced with a top-right arrow icon. 
- Narrow the first profile avatar/level card width.
- After every code change, create a Git commit and push it to the remote repository.

## General Guidelines
- Do not use PowerShell Get-Content or similar terminal file-reading commands in this workspace because they can stall or block work; use workspace file tools instead.