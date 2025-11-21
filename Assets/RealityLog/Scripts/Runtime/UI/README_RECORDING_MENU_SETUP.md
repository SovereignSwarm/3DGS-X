# Recording Menu Setup Guide (Meta XR SDK)

This guide explains how to set up the recording menu UI using Meta XR SDK's optimized VR components.

## Overview

The recording menu system uses:
- **OVROverlayCanvas** - Optimized VR rendering for Unity Canvas (shared with InfoCanvas)
- **Standard Unity UI** - Buttons, Text, ScrollView work as normal
- **Ray-based Interaction** - Controllers automatically interact with UI via ray casting

## Setup Steps

### 1. Use Existing InfoCanvas

Since InfoCanvas already has **OVROverlayCanvas** attached and is anchored to `OVRCameraRig.CenterEyeAnchor`, add the recording menu as child panels within the same Canvas.

**Important:** Only one OVROverlayCanvas can be priority at a time, so combining both UIs into the same Canvas is the recommended approach.

### 2. Create UI Hierarchy Under InfoCanvas

Add the recording menu components as children of your existing InfoCanvas:

```
InfoCanvas (with OVROverlayCanvas - already configured)
├── [Existing InfoCanvas UI elements]
│   ├── StandbyLabel
│   ├── NoPermissionLabel
│   └── ...
├── RecordingMenuPanel (NEW - initially hidden)
│   │   (GameObject with RectTransform - can have Image component for background)
│   ├── ScrollView
│   │   └── Content (assign to RecordingListUI.listContainer)
│   │       └── RecordingItemPrefab (prefab with RecordingListItemUI)
│   ├── StatusText (TextMeshProUGUI)
│   └── EmptyListMessage (GameObject)
├── RecordingIndicator (NEW - shows when recording)
│   ├── RecordingIcon (Image)
│   └── TimerText (TextMeshProUGUI)
└── RecordingSavedNotification (NEW - shows after saving)
    ├── NotificationPanel
    └── MessageText (TextMeshProUGUI)
```

### 3. Add Components

**On InfoCanvas GameObject (or a child manager GameObject):**
- `RecordingMenuController` (handles Y button input)
- `RecordingListManager` (scans for recordings)
- `RecordingOperations` (file operations)

**On RecordingMenuPanel:**
- `RecordingListUI` (main UI coordinator)

**On RecordingItemPrefab:**
- `RecordingListItemUI` component
- TextMeshProUGUI components for: directory name, date, size
- Button components for: Delete, Move, Compress, Upload

### 4. Wire Up References

In the Inspector, connect all serialized fields:

**RecordingMenuController:**
- `menuPanel`: The RecordingMenuPanel GameObject

**RecordingListUI:**
- `listManager`: Reference to RecordingListManager component
- `operations`: Reference to RecordingOperations component
- `listContainer`: The ScrollView Content Transform
- `recordingItemPrefab`: Prefab with RecordingListItemUI component
- `emptyListMessage`: GameObject to show when no recordings
- `statusText`: TextMeshProUGUI for operation status

**RecordingIndicator:**
- `recordingManager`: Reference to RecordingManager component
- `recordingIcon`: Image component for the recording icon
- `timerText`: TextMeshProUGUI for the timer

**RecordingSavedNotification:**
- `notificationPanel`: The notification panel GameObject
- `messageText`: TextMeshProUGUI for the message
- Connect `RecordingManager.onRecordingSaved` UnityEvent to `OnRecordingSaved(string)` method

### 5. Ray Interaction Setup

The Meta XR SDK automatically handles ray-based UI interaction if:
- You have OVRInteraction prefab in the scene (from Meta XR SDK)
- Canvas has proper layer settings
- Buttons use standard Unity UI Button component

**No additional setup needed** - ray casting works automatically!

## VR-Specific Considerations

### Text Readability
- Use **TextMeshPro** (not legacy Text)
- Font size: 24-32 for readable text at 2m distance
- High contrast colors (white on dark background)

### Button Size
- Minimum button size: 50x50 pixels (at 2m distance)
- Add padding between buttons for easy clicking
- Use visual feedback (hover states, pressed states)

### Performance
- OVROverlayCanvas provides better performance than standard Canvas
- Limit number of visible list items (use ScrollView pagination if needed)
- Use object pooling for list items if you have many recordings

## Layout Tips

Since InfoCanvas is anchored to CenterEyeAnchor:
- **Position**: UI elements will be relative to the camera anchor
- **Scale**: Keep scale consistent (1 unit = 1 meter)
- **Layering**: Use Canvas sorting order or Z-position to layer panels
- **Visibility**: Use GameObject.SetActive() to show/hide panels

**Recommended Layout:**
- Recording Indicator: Top-right corner (small, always visible when recording)
- Recording Menu: Center screen when opened (large panel)
- Saved Notification: Center or top-center (temporary popup)

## Testing

1. **In Editor**: OVROverlayCanvas may show differently - test on device
2. **On Device**: 
   - Press Y button to toggle menu
   - Use controller ray to interact with buttons
   - Verify text is readable at menu distance
   - Check that InfoCanvas UI and Recording Menu don't overlap when both visible

## Troubleshooting

**Menu not visible:**
- Check Canvas is active
- Verify OVROverlayCanvas is enabled
- Check Canvas distance isn't too far/close

**Buttons not clickable:**
- Ensure OVRInteraction prefab is in scene
- Check Canvas layer matches interaction settings
- Verify buttons have colliders (Unity UI handles this automatically)

**Text blurry:**
- Increase Canvas resolution in OVROverlayCanvas settings
- Use higher resolution fonts
- Adjust distance from camera

