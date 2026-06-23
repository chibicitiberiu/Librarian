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
* App controller                *
********************************/
function AppController() {
    this.statusMessage = document.getElementById("app-statusbar-message");
    this.statusProgress = document.getElementById("app-statusbar-progress");
    this.defaultStatus = this.statusMessage.innerHTML;
    this.previousJobCount = 0;

    var _this = this;
    setInterval(function () { _this.updateStatus(); }, 2000);
}

AppController.prototype.updateStatus = function () {
    var _this = this;
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () {
        if (this.readyState != 4)
            return;

        if (this.status == 200) {
            _this.onUpdateStatus(JSON.parse(this.response));
        }
    }

    request.open("GET", document.librarian_url_Jobs_GetJobsSummary);
    request.send();
}

AppController.prototype.onUpdateStatus = function (jobsSummary) {
    if (jobsSummary.runningJobCount != this.previousJobCount) {
        if (jobsSummary.runningJobCount == 0) {
            this.statusMessage.innerHTML = this.defaultStatus;
            this.statusProgress.classList.add("collapsed");
        }
        else {
            this.statusProgress.classList.remove("collapsed");
        }
        this.previousJobCount = jobsSummary.runningJobCount;
    }

    if (jobsSummary.runningJobCount > 0) {
        this.statusMessage.innerText = jobsSummary.message;
        this.statusProgress.value = jobsSummary.progress;
    }
}

function app_setup() {
    document.librarian_AppController = new AppController();
}

window.addEventListener("load", app_setup);