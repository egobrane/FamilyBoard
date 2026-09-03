# Wall-display on-screen keyboard

Family Dashboard includes an app-native keyboard for wall displays that do not have a physical keyboard. It is a frontend interaction aid: field values still use the existing forms, validation, antiforgery, authorization, and backend APIs.

## Activation

The account menu cycles through three device-local modes:

- **Auto** opens the keyboard only for an authenticated shared-display session on a wide viewport whose browser reports a coarse pointer.
- **On** forces it on for supported form fields on the current browser device.
- **Off** disables it and leaves text entry to the operating system or physical keyboard.

Only the mode is stored in browser local storage. Typed text, form values, credentials, and household data are never stored by the keyboard.

## Supported controls

Editable text, search, email, URL, telephone, number, and multiline fields are supported. The keyboard provides letters, capitalization, symbols, email shortcuts, context-appropriate numeric keys, space, backspace, newline for multiline fields, field-to-field Previous/Next controls, and Done.

Password and PIN fields are excluded. PINs continue to use their dedicated keypad. Date, time, file, select, range, radio, checkbox, disabled, read-only, and fields explicitly marked `data-touch-keyboard="off"` retain their native or existing application controls.

The first increment is English QWERTY only. Suggestions, autocorrect, dictation, handwriting, emoji browsing, and full international input-method support remain deferred. A physical keyboard continues to work normally.

## Interaction and layout

The keyboard stays fixed above the lower edge, keeps the active field visible, and temporarily yields the bottom navigation area. Pointer presses do not steal field focus. Workspace swipe navigation ignores gestures that begin on the keyboard. Escape or Done dismisses it, and reduced-motion preferences suppress its entrance motion.

## Physical staging checklist

1. Put the wall browser into shared-display mode and leave the keyboard preference on Auto.
2. Open Calendar, add an event, and confirm the keyboard opens for the title, location, and notes fields.
3. Enter mixed-case text, punctuation, spaces, a line break, and use backspace at both the end and middle of text.
4. Use Previous and Next to move between eligible fields and Done to dismiss the keyboard.
5. Confirm date, time, select, upload, and parent-PIN controls retain their native or dedicated behavior.
6. Open a numeric administration field and verify only context-appropriate sign and decimal controls appear.
7. Confirm the focused field remains visible, the navigation dock does not overlap the keyboard, vertical scrolling works, and horizontal workspace swipes do not trigger from a key press.
8. Refresh and confirm the selected Auto/On/Off mode persists, while previously typed unsaved text is not retained by the keyboard.
9. Repeat representative entry on the physical wall display and Safari. Confirm phone-sized Auto mode leaves the platform keyboard in control.
