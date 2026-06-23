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

window.addEventListener("load", setupSortableTables);

/********************************
 * Helpers                      *
 ********************************/
function wm_find_parent_by_class(element, parentClassName) {
    while (element != null && !element.classList.contains(parentClassName)) {
        element = element.parentElement;
    }

    return element;
}

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
 * Menus                        *
 ********************************/
function wm_menu_onclick(menuItem, event) {
    subMenu = menuItem.querySelector('ul');
    if (subMenu != null) {
        wm_menu_submenu_show(menuItem, subMenu);
    }
    else {
        // ensure child gets the event
        if (event.target == menuItem
            && menuItem.firstChild != null
            && menuItem.firstChild.click !== undefined) {
            menuItem.firstChild.click();
        }

        wm_menu_close(menuItem);
        event.stopPropagation();
    }
}

function wm_menu_submenu_show(menuItem, subMenu) {
    menuItem.classList.add("wm-state-active");
    subMenu.classList.add("wm-state-active");
}

function wm_menu_submenu_hide(menuItem, subMenu) {
    menuItem.classList.remove("wm-state-active");
    subMenu.classList.remove("wm-state-active");
}

function wm_menu_close(menuItem) {
    var menu = wm_find_parent_by_class(menuItem, 'wm-menu');
    if (menu != null) {
        menu.querySelectorAll('.wm-state-active')
            .forEach(item => item.classList.remove('wm-state-active'));
    }
}

function wm_menu_setup() {
    document.querySelectorAll('.wm-menu').forEach(menu => {
        menu.querySelectorAll('ul').forEach(nestedMenu => {
            nestedMenu.parentElement.addEventListener('focusout', function (event) {
                // only if no child has focus
                if (!nestedMenu.contains(event.relatedTarget)) {
                    wm_menu_submenu_hide(this, nestedMenu);
                }
            });
        });

        menu.querySelectorAll('li').forEach(menuItem => {
            menuItem.tabIndex = 0;
            menuItem.addEventListener('click', function (event) { wm_menu_onclick(this, event); });
        });
    });
}

/********************************
 * Initialization               *
 ********************************/
function wm_setup() {
    wm_fix_layout();
    wm_menu_setup();
}

window.addEventListener("load", wm_setup);