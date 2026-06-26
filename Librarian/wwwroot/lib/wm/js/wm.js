/*
 * Window-manager control library — Tier-1 progressive enhancement. Everything here is optional: the
 * chrome renders and is usable without JS (menus open on :hover, tables render unsorted). This adds
 * click/keyboard menus, right-click context menus and click-to-sort on top.
 */

/********************************
 * Sortable tables              *
 ********************************/
const getCellValue = (tr, idx) => {
    if (tr.children[idx].hasAttribute('data-sort')) {
        return tr.children[idx].getAttribute('data-sort');
    }
    else return tr.children[idx].innerText || tr.children[idx].textContent;
}

const comparer = (idx, asc) => (a, b) => ((v1, v2) =>
    v1 !== '' && v2 !== '' && !isNaN(v1) && !isNaN(v2) ? v1 - v2 : v1.toString().localeCompare(v2)
)(getCellValue(asc ? a : b, idx), getCellValue(asc ? b : a, idx));

function setupSortableTables() {
    document.querySelectorAll('table.sortable').forEach(table =>
        table.querySelectorAll('th').forEach(th => {
            th.addEventListener('click', (() => {
                const tbody = table.querySelector('tbody');
                if (tbody != null) {
                    Array.from(tbody.querySelectorAll('tr'))
                        .sort(comparer(Array.from(th.parentNode.children).indexOf(th), this.asc = !this.asc))
                        .forEach(tr => tbody.appendChild(tr));
                }
                else {
                    Array.from(table.querySelectorAll('tr:nth-child(n+2)'))
                        .sort(comparer(Array.from(th.parentNode.children).indexOf(th), this.asc = !this.asc))
                        .forEach(tr => table.appendChild(tr));
                }
            }))
        })
    )
}

/********************************
 * Layout (legacy demo support) *
 ********************************/
function wm_fix_layout() {
    var topPanel = document.querySelector('.wm-panel-top');
    if (topPanel != null) {
        document.body.style.marginTop = topPanel.clientHeight + "px";
    }
    var bottomPanel = document.querySelector('.wm-panel-bottom');
    if (bottomPanel != null) {
        document.body.style.marginBottom = bottomPanel.clientHeight + "px";
    }
}

/********************************
 * Menus & context menus        *
 ********************************/
function wmHideContextMenus() {
    document.querySelectorAll('.wm-contextmenu.wm-open').forEach(function (m) {
        m.classList.remove('wm-open');
    });
}

function wmCloseAllMenus(except) {
    document.querySelectorAll('.wm-menu-top.wm-open, .wm-menuitem-haspopup.wm-open').forEach(function (el) {
        if (el !== except) el.classList.remove('wm-open');
    });
    wmHideContextMenus();
}

function wmSetupMenubar() {
    document.querySelectorAll('.wm-menubar .wm-menu-label').forEach(function (label) {
        var top = label.parentElement; // .wm-menu-top

        label.addEventListener('click', function (e) {
            e.stopPropagation();
            var willOpen = !top.classList.contains('wm-open');
            wmCloseAllMenus();
            if (willOpen) top.classList.add('wm-open');
        });

        // When a menu is already open, hovering a sibling switches to it (classic menubar feel).
        top.addEventListener('mouseenter', function () {
            var anyOpen = document.querySelector('.wm-menubar .wm-menu-top.wm-open');
            if (anyOpen && anyOpen !== top) {
                wmCloseAllMenus();
                top.classList.add('wm-open');
            }
        });
    });

    // Submenu parents toggle on click (hover is handled by CSS).
    document.querySelectorAll('.wm-menuitem-haspopup').forEach(function (item) {
        item.addEventListener('click', function (e) {
            if (e.target.closest('.wm-menuitem') === item) {
                e.stopPropagation();
                item.classList.toggle('wm-open');
            }
        });
    });
}

function wmJoinUrl(base, path) {
    if (!base) return null;
    return base.replace(/\/+$/, '') + '/' + String(path).split('/').map(encodeURIComponent).join('/');
}

function wmContextAction(menu, action) {
    if (action === 'open') {
        var href = menu.getAttribute('data-context-href');
        if (href) window.location.href = href;
    }
    else if (action === 'properties') {
        var url = wmJoinUrl(menu.getAttribute('data-properties-url'), menu.getAttribute('data-context-path') || '');
        if (url) window.location.href = url;
    }
}

function wmShowContextMenu(menu, x, y) {
    menu.classList.add('wm-open');
    var w = menu.offsetWidth, h = menu.offsetHeight;
    menu.style.left = Math.max(0, Math.min(x, window.innerWidth - w - 4)) + 'px';
    menu.style.top = Math.max(0, Math.min(y, window.innerHeight - h - 4)) + 'px';
}

function wmSetupContextMenus() {
    document.querySelectorAll('[data-wm-contextmenu], [data-wm-contextmenu-empty]').forEach(function (host) {
        var fileMenu = document.getElementById(host.getAttribute('data-wm-contextmenu') || '');
        var emptyMenu = document.getElementById(host.getAttribute('data-wm-contextmenu-empty') || '');

        host.addEventListener('contextmenu', function (e) {
            var item = e.target.closest('.wm-filelist-item');
            wmCloseAllMenus();

            if (item && fileMenu) {
                // Over an item → file menu. Select it (keeping an existing multi-selection it's part of)
                // and remember its target for the Open/Properties actions.
                e.preventDefault();
                var fl = item.closest('[data-wm-filelist]');
                if (!item.classList.contains('wm-selected')) {
                    if (fl) wmClearSelection(fl);
                    wmSetItemSelected(item, true);
                    if (fl) wmUpdateSelectionInfo(fl);
                }
                var link = item.querySelector('a');
                fileMenu.setAttribute('data-context-href', link ? link.href : '');
                fileMenu.setAttribute('data-context-path', item.getAttribute('data-path') || '');
                wmShowContextMenu(fileMenu, e.clientX, e.clientY);
            } else if (emptyMenu) {
                // Over empty space → folder menu; clear the selection first.
                e.preventDefault();
                var fl2 = host.querySelector('[data-wm-filelist]');
                if (fl2) { wmClearSelection(fl2); wmUpdateSelectionInfo(fl2); }
                wmShowContextMenu(emptyMenu, e.clientX, e.clientY);
            }
        });

        // Left-click on empty space (not an item, not inside a popup) clears the selection.
        host.addEventListener('click', function (e) {
            if (e.target.closest('.wm-filelist-item') || e.target.closest('.wm-menu-popup')) return;
            var fl = host.querySelector('[data-wm-filelist]');
            if (fl) { wmClearSelection(fl); wmUpdateSelectionInfo(fl); }
        });

        // Wire data-action items in both menus.
        [fileMenu, emptyMenu].forEach(function (menu) {
            if (!menu) return;
            menu.querySelectorAll('[data-action]').forEach(function (it) {
                it.addEventListener('click', function () { wmContextAction(menu, it.getAttribute('data-action')); });
            });
        });
    });
}

function wmSetupGlobalClose() {
    document.addEventListener('click', function () { wmCloseAllMenus(); });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' || e.keyCode === 27) {
            wmCloseAllMenus();
            // Esc also cancels the top-most open modal.
            document.querySelectorAll('.wm-modal-backdrop.wm-open').forEach(function (bd) {
                wmCloseModal(bd, 'cancel');
            });
        }
    });
    window.addEventListener('blur', function () { wmCloseAllMenus(); });
}

/********************************
 * Modals                       *
 ********************************/
function wmSetMainWindowInactive(inactive) {
    // The single maximised window dims its title bar while a modal is on top.
    var win = document.querySelector('.wm-window');
    if (win) {
        if (inactive) win.classList.add('wm-window-inactive');
        else win.classList.remove('wm-window-inactive');
    }
}

function wmOpenModal(id) {
    var bd = document.getElementById(id);
    if (!bd || !bd.classList.contains('wm-modal-backdrop')) return;
    bd.classList.add('wm-open');
    bd.setAttribute('aria-hidden', 'false');
    wmSetMainWindowInactive(true);
    var focusTarget = bd.querySelector('.wm-modal-actions .wm-button, [data-wm-close]');
    if (focusTarget) focusTarget.focus();
}

function wmCloseModal(bd, result) {
    bd.classList.remove('wm-open');
    bd.setAttribute('aria-hidden', 'true');
    if (!document.querySelector('.wm-modal-backdrop.wm-open')) {
        wmSetMainWindowInactive(false);
    }
    try {
        bd.dispatchEvent(new CustomEvent('wm:modalresult', {
            bubbles: true,
            detail: { id: bd.id, result: result }
        }));
    } catch (e) { /* CustomEvent unsupported — result event simply not emitted */ }
}

function wmSetupModals() {
    document.querySelectorAll('[data-wm-open-modal]').forEach(function (trigger) {
        trigger.addEventListener('click', function () {
            wmOpenModal(trigger.getAttribute('data-wm-open-modal'));
        });
    });

    document.querySelectorAll('.wm-modal-backdrop').forEach(function (bd) {
        bd.querySelectorAll('[data-wm-result]').forEach(function (btn) {
            btn.addEventListener('click', function () { wmCloseModal(bd, btn.getAttribute('data-wm-result')); });
        });
        bd.querySelectorAll('[data-wm-close]').forEach(function (btn) {
            btn.addEventListener('click', function () { wmCloseModal(bd, 'cancel'); });
        });
        // Click on the backdrop itself (outside the dialog) cancels.
        bd.addEventListener('click', function (e) {
            if (e.target === bd) wmCloseModal(bd, 'cancel');
        });
    });
}

/********************************
 * FileListView selection       *
 ********************************/
function wmItemCheckbox(item) {
    return item.querySelector('.wm-filelist-check');
}

function wmSetItemSelected(item, on) {
    if (on) item.classList.add('wm-selected');
    else item.classList.remove('wm-selected');
    var cb = wmItemCheckbox(item);
    if (cb) cb.checked = on;
}

function wmClearSelection(container) {
    container.querySelectorAll('.wm-filelist-item.wm-selected').forEach(function (it) {
        wmSetItemSelected(it, false);
    });
}

function wmUpdateSelectionInfo(container) {
    var n = container.querySelectorAll('.wm-filelist-item.wm-selected').length;
    document.querySelectorAll('[data-wm-selection-count]').forEach(function (el) {
        el.textContent = n ? (n + ' selected') : '';
    });
    document.querySelectorAll('[data-wm-needs-selection]').forEach(function (el) {
        if (n) { el.classList.remove('disabled'); if ('disabled' in el) el.disabled = false; }
        else { el.classList.add('disabled'); if ('disabled' in el) el.disabled = true; }
    });
}

function wmSetupFileLists() {
    document.querySelectorAll('[data-wm-filelist]').forEach(function (container) {
        if (!container.classList.contains('wm-filelist-selectable')) return;
        var items = function () { return Array.prototype.slice.call(container.querySelectorAll('.wm-filelist-item')); };
        var lastIndex = -1;

        // Per-row checkbox.
        container.querySelectorAll('.wm-filelist-check').forEach(function (cb) {
            cb.addEventListener('click', function (e) { e.stopPropagation(); });
            cb.addEventListener('change', function () {
                var item = cb.closest('.wm-filelist-item');
                if (!item) return;
                wmSetItemSelected(item, cb.checked);
                lastIndex = items().indexOf(item);
                wmUpdateSelectionInfo(container);
            });
        });

        // Select-all (details header).
        var all = container.querySelector('.wm-filelist-all');
        if (all) {
            all.addEventListener('click', function (e) { e.stopPropagation(); });
            all.addEventListener('change', function () {
                items().forEach(function (it) { wmSetItemSelected(it, all.checked); });
                wmUpdateSelectionInfo(container);
            });
        }

        // Ctrl/Shift click on an item's name modifies selection instead of navigating.
        container.querySelectorAll('.wm-filelist-item a').forEach(function (link) {
            link.addEventListener('click', function (e) {
                var item = link.closest('.wm-filelist-item');
                if (!item) return;
                var list = items();
                var idx = list.indexOf(item);
                if (e.shiftKey) {
                    e.preventDefault();
                    var from = lastIndex < 0 ? idx : lastIndex;
                    var lo = Math.min(from, idx), hi = Math.max(from, idx);
                    wmClearSelection(container);
                    for (var i = lo; i <= hi; i++) wmSetItemSelected(list[i], true);
                    wmUpdateSelectionInfo(container);
                } else if (e.ctrlKey || e.metaKey) {
                    e.preventDefault();
                    wmSetItemSelected(item, !item.classList.contains('wm-selected'));
                    lastIndex = idx;
                    wmUpdateSelectionInfo(container);
                }
                // plain click: let the link navigate.
            });
        });

        // Type-to-jump: focus the list, type a name prefix.
        var buffer = '', bufferTimer = null;
        container.addEventListener('keydown', function (e) {
            if (e.ctrlKey || e.metaKey || e.altKey) return;
            if (e.key && e.key.length === 1 && /\S/.test(e.key)) {
                buffer += e.key.toLowerCase();
                if (bufferTimer) clearTimeout(bufferTimer);
                bufferTimer = setTimeout(function () { buffer = ''; }, 800);
                var match = items().filter(function (it) {
                    return (it.getAttribute('data-name') || '').toLowerCase().indexOf(buffer) === 0;
                })[0];
                if (match) {
                    wmClearSelection(container);
                    wmSetItemSelected(match, true);
                    lastIndex = items().indexOf(match);
                    wmUpdateSelectionInfo(container);
                    if (match.scrollIntoView) match.scrollIntoView({ block: 'nearest' });
                }
            }
        });

        // Reflect the initial (empty) selection so selection-dependent controls start disabled.
        wmUpdateSelectionInfo(container);
    });
}

/********************************
 * Taskbar jobs tray            *
 ********************************/
function wmSetupTray() {
    var icon = document.getElementById('wm-tray-jobs');
    if (!icon || !window.wm_url_jobs) return;
    var badge = icon.querySelector('.wm-tray-badge');

    function poll() {
        var r = new XMLHttpRequest();
        r.onreadystatechange = function () {
            if (r.readyState !== 4 || r.status !== 200) return;
            try {
                var s = JSON.parse(r.responseText);
                var n = s.runningJobCount || 0;
                if (n > 0) {
                    icon.classList.add('wm-busy');
                    icon.title = 'Background jobs: ' + (s.message || (n + ' running'));
                    if (badge) badge.textContent = n;
                } else {
                    icon.classList.remove('wm-busy');
                    icon.title = 'Background jobs: idle';
                    if (badge) badge.textContent = '';
                }
            } catch (e) { /* ignore malformed responses */ }
        };
        r.open('GET', window.wm_url_jobs);
        r.send();
    }

    poll();
    // Polling (not websockets) by design for now — cheap and old-browser friendly.
    setInterval(poll, 2500);
}

/********************************
 * Initialization               *
 ********************************/
function wm_setup() {
    wm_fix_layout();
    setupSortableTables();
    wmSetupMenubar();
    wmSetupFileLists();
    wmSetupContextMenus();
    wmSetupModals();
    wmSetupTray();
    wmSetupGlobalClose();
}

window.addEventListener("load", wm_setup);
