mergeInto(LibraryManager.library,
    {
        // GUIUtility.systemCopyBuffer only fills the buffer inside the player: the browser clipboard is
        // written through the page. The call comes from a game frame, not from the DOM event handler, so it
        // relies on the transient user activation left by the click.
        WebClipboardCopy: function (textPtr) {
            const text = UTF8ToString(textPtr);

            // navigator.clipboard exists only in a secure context (https or localhost).
            if (navigator.clipboard !== undefined) {
                navigator.clipboard.writeText(text).catch(function (e) {
                    console.error('[Clipboard] Failed to copy: ' + e);
                });

                return;
            }

            const active = document.activeElement;
            const area = document.createElement('textarea');
            area.value = text;
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();

            const isCopied = document.execCommand('copy');

            document.body.removeChild(area);

            if (active !== null)
                active.focus();

            if (isCopied === false)
                console.error('[Clipboard] Failed to copy: execCommand rejected');
        }
    });
