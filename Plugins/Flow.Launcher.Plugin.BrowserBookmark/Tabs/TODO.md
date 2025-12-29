# Items to consider to improve the code

- TabsWalker.GetCurrentTabFromWindow
    - Research browsers' settings and check if it may break current assumption of just taking the last tab

- absTracker
    - AutomationFocusChangedEventHandler could be replaced with AutomationStructureChangedEventHandler, StructureChangeType.ChildAdded
    - Removal of tabs should be handled to save memory by using AutomationStructureChangedEventHandler, StructureChangeType.ChildRemoved
