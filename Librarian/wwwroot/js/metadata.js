// Item Viewer (Metadata page) enhancement. Tier-0 works without this: Save is a real form submit and
// every field is a plain input. This adds the Add/Delete field controls. It must never throw on load
// (a previous version was a stray copy of browse.js and crashed, killing all page JS).

(function () {
    function setStatus(message) {
        var els = document.querySelectorAll("[data-wm-status]");
        if (els.length) { els.forEach(function (el) { el.textContent = message; }); return; }
        var el = document.getElementById("window-statusbar-message");
        if (el) el.innerText = message;
    }

    function MetadataEditor(form) {
        this.form = form;
        var _this = this;

        this.btnAdd = document.getElementById("btn-add");
        this.btnDelete = document.getElementById("btn-delete");
        this.optionsTemplate = document.getElementById("metadata-field-options");

        if (this.btnAdd) {
            this.btnAdd.addEventListener("click", function () { _this.addField(); });
        }
        if (this.btnDelete) {
            this.btnDelete.addEventListener("click", function () { _this.deleteSelected(); });
        }

        // Selecting rows toggles the Delete button. Wire existing (server-rendered) checkboxes.
        var boxes = this.form.querySelectorAll("input.field-select");
        for (var i = 0; i < boxes.length; i++) {
            boxes[i].addEventListener("click", function () { _this.refreshButtons(); });
        }

        this.refreshButtons();
    }

    MetadataEditor.prototype.selectedRows = function () {
        var rows = [];
        var boxes = this.form.querySelectorAll("input.field-select");
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].checked && !boxes[i].disabled) {
                var tr = boxes[i];
                while (tr && tr.tagName !== "TR") tr = tr.parentNode;
                if (tr) rows.push(tr);
            }
        }
        return rows;
    };

    MetadataEditor.prototype.refreshButtons = function () {
        if (this.btnDelete) this.btnDelete.disabled = this.selectedRows().length === 0;
    };

    MetadataEditor.prototype.deleteSelected = function () {
        var rows = this.selectedRows();
        for (var i = 0; i < rows.length; i++) {
            rows[i].parentNode.removeChild(rows[i]);
        }
        this.refreshButtons();
        if (rows.length > 0) {
            setStatus(rows.length + " field" + (rows.length === 1 ? "" : "s") + " removed — Save to apply.");
        }
    };

    MetadataEditor.prototype.addField = function () {
        if (!this.optionsTemplate) return;

        var group = document.getElementById("metadata-added");
        if (!group) return;
        group.style.display = "";
        var tbody = group.getElementsByTagName("tbody")[0];

        var _this = this;
        var tr = document.createElement("tr");
        tr.setAttribute("data-id", "new");

        // selection checkbox
        var tdChk = document.createElement("td");
        var chk = document.createElement("input");
        chk.type = "checkbox";
        chk.className = "field-select";
        chk.addEventListener("click", function () { _this.refreshButtons(); });
        tdChk.appendChild(chk);

        // attribute-definition picker (cloned from the server-rendered <select>)
        var tdName = document.createElement("td");
        var sel = this.optionsTemplate.cloneNode(true);
        sel.id = "";
        sel.className = "add-field-def";
        sel.style.display = "";
        sel.removeAttribute("aria-hidden");
        tdName.appendChild(sel);

        // value input — its name follows the chosen definition so it posts as "Group/Name"
        var tdVal = document.createElement("td");
        tdVal.className = "col-input";
        var val = document.createElement("input");
        val.type = "text";
        tdVal.appendChild(val);

        function syncName() { val.name = sel.value; }
        sel.addEventListener("change", syncName);
        syncName();

        var tdFill = document.createElement("td");
        tdFill.className = "fill";

        tr.appendChild(tdChk);
        tr.appendChild(tdName);
        tr.appendChild(tdVal);
        tr.appendChild(tdFill);
        tbody.appendChild(tr);

        val.focus();
        this.refreshButtons();
    };

    window.addEventListener("load", function () {
        var form = document.getElementById("metadata-form");
        if (form) new MetadataEditor(form);
    });
})();
