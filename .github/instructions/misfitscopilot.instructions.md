---
description: Describe when these instructions should be loaded by the agent based on task context
# applyTo: 'Describe when these instructions should be loaded by the agent based on task context' # when provided, instructions will automatically be added to the request context when the pattern matches an attached file
---

<!-- Tip: Use /create-instructions in chat to generate content with agent assistance -->

All changes in codebase not in a _Misfits or Misfits folder is to be annotated as "#Misfits Change /Add:/Tweak:/Fix:/ (Where relevant)", to include custom .cs file edits, or standing .cs file edits. If we can create/edit/modify any and all additions we are to move them to a relevant _Misfits or Misfits folder. If we cannot move them to a relevant "_Misfits or Misfits folder", we are to create a new one and move them there. This is to ensure that all changes are easily identifiable and organized within the codebase relevant to "Misfits" changes. Any new files like .cs made, need to have annotation depicting it came from Misfits, like, "#Misfits Change /Add:/Tweak:/Fix:/ (Where relevant)" at the top of the file. This is to ensure that all changes are easily identifiable and organized within the codebase relevant to Misfits changes.

Never DELETE files, only always comment out if they are no longer relevant. This is to ensure that we can easily track changes and revert if necessary, while maintaining a clear history of modifications within the codebase. By commenting out instead of deleting, we preserve the context and rationale behind changes, which can be crucial for future reference and debugging.

It is recommended to always make comments to new additions for future logic and understanding relevancy. This is to ensure that future developers can easily understand the purpose and functionality of new code, facilitating better collaboration and maintenance of the codebase. Providing clear comments helps to clarify the intent behind code changes and can significantly improve the readability and maintainability of the project over time.
