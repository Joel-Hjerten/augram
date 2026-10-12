# CommandsWorkbench

The layout of a Commands sub-tab: the tree (`PART_Tree`) beside the selected command's header (`PART_Header`) and step list (`PART_Steps`), or the side panel's form (an app group, a hold remap, a category). It passes the view model's data down (`SelectedCommand` is a `CommandHeaderItem`; its step refusals go to the step list) and forwards the parts' events up. No rules of its own.
