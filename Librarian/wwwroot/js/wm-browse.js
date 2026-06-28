/*
 * Browse file operations — Tier-1 enhancement over the wm FileListView. Cut / Copy / Paste / Rename /
 * Delete drive the Browse controller's JSON endpoints; the selection comes from the wm file list
 * (wm.js maintains `.wm-selected` on items, each carrying data-name + data-path). Without this script
 * the chrome still renders and navigates — only the editing actions are inert.
 */
(function () {
    var cfg = window.browse;
    if (!cfg) return;

    function setStatus(msg) {
        document.querySelectorAll('[data-wm-status]').forEach(function (el) { el.textContent = msg || ''; });
    }

    function selectedItems() {
        return Array.prototype.slice.call(document.querySelectorAll('.wm-filelist-item.wm-selected'));
    }
    function selectedNames() {
        return selectedItems().map(function (it) { return it.getAttribute('data-name'); }).filter(Boolean);
    }
    function selectedPaths() {
        return selectedItems().map(function (it) { return it.getAttribute('data-path'); }).filter(Boolean);
    }

    function postJson(url, body, onOk, onErr) {
        var r = new XMLHttpRequest();
        r.onreadystatechange = function () {
            if (r.readyState !== 4) return;
            if (r.status === 200) { if (onOk) onOk(r.responseText); }
            else {
                var msg = r.responseText;
                try { msg = JSON.parse(r.responseText).message || msg; } catch (e) { /* plain text */ }
                if (onErr) onErr(msg);
            }
        };
        r.open('POST', url);
        r.setRequestHeader('Content-Type', 'application/json;charset=UTF-8');
        r.send(JSON.stringify(body));
    }

    function markClipboard(op, names) {
        document.querySelectorAll('.wm-filelist-item').forEach(function (it) {
            it.classList.remove('wm-cut', 'wm-copied');
            if (names.indexOf(it.getAttribute('data-name')) >= 0)
                it.classList.add(op === 'cut' ? 'wm-cut' : 'wm-copied');
        });
        // Clipboard now has content → enable Paste everywhere it appears.
        document.querySelectorAll('[data-action="paste"]').forEach(function (el) {
            el.classList.remove('disabled');
            if ('disabled' in el) el.disabled = false;
        });
    }

    function doCut() {
        var names = selectedNames();
        if (!names.length) return;
        setStatus('Cutting…');
        postJson(cfg.urlCut, { path: cfg.path, items: names },
            function () { markClipboard('cut', names); setStatus(names.length + ' item(s) cut'); },
            function (m) { setStatus('Cut failed: ' + m); });
    }

    function doCopy() {
        var names = selectedNames();
        if (!names.length) return;
        setStatus('Copying…');
        postJson(cfg.urlCopy, { path: cfg.path, items: names },
            function () { markClipboard('copy', names); setStatus(names.length + ' item(s) copied'); },
            function (m) { setStatus('Copy failed: ' + m); });
    }

    function doPaste() {
        setStatus('Pasting…');
        postJson(cfg.urlPaste, { path: cfg.path },
            function () { window.location.reload(); },
            function (m) { setStatus('Paste failed: ' + m); });
    }

    function doDelete() {
        var names = selectedNames();
        if (!names.length) return;
        setStatus('Deleting…');
        postJson(cfg.urlDelete, { path: cfg.path, items: names },
            function () { window.location.reload(); },
            function (m) { setStatus('Delete failed: ' + m); });
    }

    function doRename(newName) {
        var names = selectedNames();
        if (names.length !== 1 || !newName || newName === names[0]) return;
        setStatus('Renaming…');
        postJson(cfg.urlRename, { path: cfg.path, item: names[0], newName: newName },
            function () { window.location.reload(); },
            function (m) { setStatus('Rename failed: ' + m); });
    }

    function doMetadata() {
        var paths = selectedPaths();
        if (!paths.length) return;
        window.location.href = cfg.urlMetadata.replace(/\/+$/, '') + '/' +
            paths[0].split('/').map(encodeURIComponent).join('/');
    }

    // Open a dialog, then act on a positive ('confirm'/'ok') result. The handler is one-shot.
    function openModalFlow(id, before, onConfirm) {
        var bd = document.getElementById(id);
        if (!bd) return;
        if (before) before(bd);
        if (window.wmOpenModal) window.wmOpenModal(id);
        var handler = function (e) {
            bd.removeEventListener('wm:modalresult', handler);
            var result = e.detail && e.detail.result;
            if (result === 'confirm' || result === 'ok') onConfirm(bd);
        };
        bd.addEventListener('wm:modalresult', handler);
    }

    function renameFlow() {
        var names = selectedNames();
        if (names.length !== 1) return;
        openModalFlow('modal-rename',
            function () {
                var inp = document.getElementById('browse-rename-input');
                if (inp) { inp.value = names[0]; setTimeout(function () { inp.focus(); inp.select(); }, 0); }
            },
            function () {
                var inp = document.getElementById('browse-rename-input');
                doRename(inp ? inp.value.trim() : '');
            });
    }

    function deleteFlow() {
        var names = selectedNames();
        if (!names.length) return;
        var txt = document.getElementById('browse-delete-text');
        if (txt) txt.textContent = 'Delete ' + names.length + ' item' + (names.length === 1 ? '' : 's') +
            '? This cannot be undone.';
        openModalFlow('modal-delete', null, function () { doDelete(); });
    }

    var ACTIONS = {
        cut: doCut, copy: doCopy, paste: doPaste,
        rename: renameFlow, 'delete': deleteFlow, metadata: doMetadata
    };

    function wire() {
        // Enter in the rename box confirms the dialog.
        var inp = document.getElementById('browse-rename-input');
        if (inp) inp.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                var bd = document.getElementById('modal-rename');
                var ok = bd && bd.querySelector('[data-wm-result="confirm"]');
                if (ok) ok.click();
            }
        });

        // Wire every file-op trigger (toolbar buttons, menubar items, context-menu items). wm.js owns
        // the 'open'/'properties' context actions, so we ignore those here.
        document.querySelectorAll('[data-action]').forEach(function (el) {
            var act = el.getAttribute('data-action');
            if (!ACTIONS.hasOwnProperty(act)) return;
            el.addEventListener('click', function (e) {
                if (el.classList.contains('disabled') || el.disabled) return;
                e.preventDefault();
                ACTIONS[act]();
            });
        });
    }

    window.addEventListener('load', wire);
})();
