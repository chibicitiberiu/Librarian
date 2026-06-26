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

function wmSetupContextMenus() {
    document.querySelectorAll('[data-wm-contextmenu]').forEach(function (host) {
        var menuId = host.getAttribute('data-wm-contextmenu');

        host.addEventListener('contextmenu', function (e) {
            var menu = document.getElementById(menuId);
            if (!menu) return;
            e.preventDefault();
            wmCloseAllMenus();

            // Right-click auto-selects the row under the cursor and remembers its target.
            var row = e.target.closest('[data-wm-selectable], .wm-filelist-row');
            if (row) {
                host.querySelectorAll('.wm-selected').forEach(function (r) { r.classList.remove('wm-selected'); });
                row.classList.add('wm-selected');
                var link = row.querySelector('a');
                menu.setAttribute('data-context-href', link ? link.href : '');
                menu.setAttribute('data-context-path', row.getAttribute('data-path') || '');
            }

            menu.classList.add('wm-open');
            var w = menu.offsetWidth, h = menu.offsetHeight;
            var x = Math.min(e.clientX, window.innerWidth - w - 4);
            var y = Math.min(e.clientY, window.innerHeight - h - 4);
            menu.style.left = Math.max(0, x) + 'px';
            menu.style.top = Math.max(0, y) + 'px';
        });

        // Wire data-action items inside this host's context menu.
        var menu = document.getElementById(menuId);
        if (menu) {
            menu.querySelectorAll('[data-action]').forEach(function (item) {
                item.addEventListener('click', function () { wmContextAction(menu, item.getAttribute('data-action')); });
            });
        }
    });
}

function wmSetupGlobalClose() {
    document.addEventListener('click', function () { wmCloseAllMenus(); });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' || e.keyCode === 27) wmCloseAllMenus();
    });
    window.addEventListener('blur', function () { wmCloseAllMenus(); });
}

/********************************
 * Initialization               *
 ********************************/
function wm_setup() {
    wm_fix_layout();
    setupSortableTables();
    wmSetupMenubar();
    wmSetupContextMenus();
    wmSetupGlobalClose();
}

window.addEventListener("load", wm_setup);
