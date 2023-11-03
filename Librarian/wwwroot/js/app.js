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
