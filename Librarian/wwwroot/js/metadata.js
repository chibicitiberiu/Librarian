function MetadataController() {

    this.btnCut = document.getElementById("btn-cut");
    this.btnCopy = document.getElementById("btn-copy");
    this.btnPaste = document.getElementById("btn-paste");
    this.btnRename = document.getElementById("btn-rename");
    this.btnDelete = document.getElementById("btn-delete");
    this.btnMetadata = document.getElementById("btn-metadata");

    this.chkSelectAll = document.getElementById("chk-select-all");

    this.fileListing = document.getElementById("file-listing")
    this.fileEntries = this.fileListing.getElementsByClassName("file-entry");

    this.itemBeingRenamed = null;
    this.itemBeingRenamedSavedChildren = [];
    this.txtRename = null;

    var _this = this;

    // register event handlers
    this.btnCut.addEventListener("click", function () { _this.cut(); });
    this.btnCopy.addEventListener("click", function () { _this.copy(); });
    this.btnPaste.addEventListener("click", function () { _this.paste(); });
    this.btnRename.addEventListener("click", function () { _this.rename(); });
    this.btnDelete.addEventListener("click", function () { _this.delete(); });
    this.btnMetadata.addEventListener("click", function () { _this.editMetadata(); });

    this.chkSelectAll.addEventListener("click", function () {
        _this.selectAll();
        _this.onCheckboxClicked();
    });

    for (var i = 0; i < this.fileEntries.length; i++) {
        this.fileEntries[i].querySelector("#chk-selected").addEventListener("click", function () {
            _this.onCheckboxClicked();
        })
    }

    // update state of buttons
    this.onCheckboxClicked();
}

MetadataController.prototype.setStatusMessage = function (message) {
    document.getElementById("msg-status").innerText = message;
}

MetadataController.prototype.getFileSelected = function (fileEntry) {
    return fileEntry.querySelector("#chk-selected").checked;
}

MetadataController.prototype.setFileSelected = function (fileEntry, value) {
    return fileEntry.querySelector("#chk-selected").checked = value;
}

MetadataController.prototype.setFileCut = function (fileEntry, isCut) {
    if (isCut) {
        fileEntry.classList.add("file-cut");
    }
    else {
        fileEntry.classList.remove("file-cut");
    }
}

MetadataController.prototype.setFileCopied = function (fileEntry, isCopied) {
    if (isCopied) {
        fileEntry.classList.add("file-copied");
    }
    else {
        fileEntry.classList.remove("file-copied");
    }
}

MetadataController.prototype.selectAll = function () {
    for (var i = 0; i < this.fileEntries.length; i++) {
        this.setFileSelected(this.fileEntries[i], this.chkSelectAll.checked);
    }
}

MetadataController.prototype.getSelectedFiles = function () {
    var selection = [];
    for (var i = 0; i < this.fileEntries.length; i++) {
        if (this.getFileSelected(this.fileEntries[i])) {
            selection.push(this.fileEntries[i]);
        }
    }
    return selection;
}

MetadataController.prototype.getSelectedFileIds = function () {
    var selection = [];
    for (var i = 0; i < this.fileEntries.length; i++) {
        if (this.getFileSelected(this.fileEntries[i])) {
            selection.push(this.fileEntries[i].getAttribute("data-id"));
        }
    }
    return selection;
}

MetadataController.prototype.onClipboardChanged = function (operation, newItems) {

    // update file style
    for (var i = 0; i < this.fileEntries.length; i++) {

        var fileCut = false;
        var fileCopied = false;

        // figure out if fileEntry is in the list
        var path = document.librarian_browse_path;
        if (!path.endsWith("/"))
            path = path + "/";
        path = path + this.fileEntries[i].getAttribute("data-id");

        if (newItems.includes(path)) {
            if (operation == "cut") {
                fileCut = true;
            }
            else if (operation == "copy") {
                fileCopied = true;
            }
        }

        // update
        this.setFileCut(this.fileEntries[i], fileCut);
        this.setFileCopied(this.fileEntries[i], fileCopied);
    }

    // update "x items in clipboard" message
    var imgClipboardIndicator = document.getElementById("img-clipboard-indicator");
    if (newItems.length > 0) {
        var cutCopied = (operation == "cut") ? "cut" : "copied";
        var message = newItems.length.toString() + " item(s) " + cutCopied + " in clipboard";
        imgClipboardIndicator.title = message;
        imgClipboardIndicator.classList.remove("disabled");
        this.btnPaste.disabled = false;
    }
    else {
        imgClipboardIndicator.title = "Clipboard empty";
        imgClipboardIndicator.classList.add("disabled");
        this.btnPaste.disabled = true;
    }
}

MetadataController.prototype.cut = function () {
    var selectedItems = this.getSelectedFileIds();
    if (selectedItems.length == 0)
        return;

    this.setStatusMessage("Cutting items...");

    var _this = this;
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () { _this.onCutCompleted(this); };
    request.open("POST", document.librarian_browse_urlCut);
    request.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    request.send(JSON.stringify({
        path: document.librarian_browse_path,
        items: selectedItems
    }));
}

MetadataController.prototype.onCutCompleted = function (request) {
    if (request.readyState != 4)
        return;

    if (request.status == 200) {
        this.onClipboardChanged("cut", request.response);
        this.setStatusMessage("Ready");
    }
    else {
        var resp = JSON.parse(request.response);
        this.setStatusMessage("Cutting failed: " + resp.message);
    }
}

MetadataController.prototype.copy = function () {
    var selectedItems = this.getSelectedFileIds();
    if (selectedItems.length == 0)
        return;

    this.setStatusMessage("Copying items...");

    var _this = this;
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () { _this.onCopyCompleted(this); };
    request.open("POST", document.librarian_browse_urlCopy);
    request.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    request.send(JSON.stringify({
        path: document.librarian_browse_path,
        items: selectedItems
    }));
}

MetadataController.prototype.onCopyCompleted = function (request) {
    if (request.readyState != 4)
        return;

    if (request.status == 200) {
        this.onClipboardChanged("copy", request.response);
        this.setStatusMessage("Ready");
    }
    else {
        var resp = JSON.parse(request.response);
        this.setStatusMessage("Copying failed: " + resp.message);
    }
}

MetadataController.prototype.paste = function () {
    this.setStatusMessage("Pasting items...");

    var _this = this;
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () { _this.onPasteCompleted(this); };
    request.open("POST", document.librarian_browse_urlPaste);
    request.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    request.send(JSON.stringify({
        path: document.librarian_browse_path
    }));
}

MetadataController.prototype.onPasteCompleted = function (request) {
    if (request.readyState != 4)
        return;

    if (request.status == 200) {
        // reload window
        location.reload();
    }
    else {
        var resp = JSON.parse(request.response);
        this.setStatusMessage("Pasting failed: " + resp.message);
    }
}

MetadataController.prototype.findItemColumnIndex = function (columnName) {
    var headers = this.fileListing.getElementsByTagName("th");
    var index;

    for (index = 0; index < headers.length; index++) {
        if (headers[index].innerText.toLowerCase() == columnName.toLowerCase()) {
            return index;
        }
    }

    return null;
}

MetadataController.prototype.rename = function () {
    var selectedItems = this.getSelectedFiles();
    if (selectedItems.length == 0)
        return;

    if (selectedItems.length == 1) {
        this.renameSingleItem(selectedItems[0]);
    }
    else {
        // todo: navigate to other page
    }
}

MetadataController.prototype.renameSingleItem = function (itemToRename) {
    var _this = this;

    // find column
    var nameColIdx = this.findItemColumnIndex("File");
    if (!nameColIdx)
        nameColIdx = this.findItemColumnIndex("Name");

    // create edit box
    this.txtRename = document.createElement("input");
    this.txtRename.id = "txt-rename";
    this.txtRename.type = "text";
    this.txtRename.value = itemToRename.getAttribute("data-id");
    this.txtRename.addEventListener("keyup", function (event) { _this.onTxtRenameKeyUp(this, event); });
    this.txtRename.addEventListener("focusout", function (event) { _this.onTxtRenameLostFocus(this, event); });

    // replace file name with edit box
    var cols = itemToRename.getElementsByTagName("td");

    var children = cols[nameColIdx].children;
    for (var i = 0; i < children.length; i++)
        this.itemBeingRenamedSavedChildren.push(children[i]);

    cols[nameColIdx].replaceChildren(this.txtRename);

    this.itemBeingRenamed = itemToRename;

    // focus text box
    this.txtRename.focus();
}

MetadataController.prototype.onTxtRenameKeyUp = function (sender, event) {
    if (event.key == "Enter") {
        this.completeRenameOperation();
    }
    else if (event.key == "Escape") {
        this.cancelRenameOperation();
    }
}

MetadataController.prototype.onTxtRenameLostFocus = function (sender, event) {
    var oldName = this.itemBeingRenamed.getAttribute("data-id");
    var newName = this.txtRename.value;

    if (oldName != newName)
        this.completeRenameOperation();
    else this.cancelRenameOperation();
}

MetadataController.prototype.completeRenameOperation = function () {
    this.txtRename.disabled = true;

    // perform rename
    this.setStatusMessage("Renaming...");

    var _this = this;
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () { _this.onCompleteRenameOperationCompleted(this); };
    request.open("POST", document.librarian_browse_urlRename);
    request.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    request.send(JSON.stringify({
        path: document.librarian_browse_path,
        item: this.itemBeingRenamed.getAttribute("data-id"),
        newName: this.txtRename.value
    }));
}

MetadataController.prototype.onCompleteRenameOperationCompleted = function (request) {
    if (request.readyState != 4)
        return;

    if (request.status == 200) {
        // reload window
        location.reload();
    }
    else {
        var resp = JSON.parse(request.response);
        this.setStatusMessage("Rename failed: " + resp.message);
        this.txtRename.disabled = false;
        this.txtRename.classList.add("invalid");
    }
}

MetadataController.prototype.cancelRenameOperation = function () {
    // find column
    var nameColIdx = this.findItemColumnIndex("File");
    if (!nameColIdx)
        nameColIdx = this.findItemColumnIndex("Name");

    // restore old children
    var cols = this.itemBeingRenamed.getElementsByTagName("td");
    cols[nameColIdx].replaceChildren();
    for (child of this.itemBeingRenamedSavedChildren)
        cols[nameColIdx].appendChild(child);

    this.itemBeingRenamed = null;
    this.itemBeingRenamedSavedChildren = [];
}


MetadataController.prototype.delete = function () {
    var payload = {
        "path": document.librarian_browse_path,
        "items": this.getSelectedFileIds()
    };
    if (payload.items.length == 0)
        return;

    var request = new XMLHttpRequest();
    var _this = this;
    this.setStatusMessage("Deleting items...");

    request.onreadystatechange = function () {
        if (this.readyState != 4)
            return;

        if (this.status == 200) {
            // reload window
            location.reload();
        }
        else {
            var resp = JSON.parse(this.response);
            _this.setStatusMessage("Deleting failed: " + resp.message);
        }
    };

    request.open("POST", document.librarian_browse_urlDelete);
    request.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    request.send(JSON.stringify(payload));
}

MetadataController.prototype.editMetadata = function () {
    var url = document.librarian_browse_urlMetadata + "/" + this.getSelectedFileIds()[0];
    location.href = url;
}

MetadataController.prototype.onCheckboxClicked = function () {
    var selection = this.getSelectedFileIds();
    this.btnCut.disabled = (selection.length == 0);
    this.btnCopy.disabled = (selection.length == 0);
    this.btnRename.disabled = (selection.length == 0);
    this.btnDelete.disabled = (selection.length == 0);
    this.btnMetadata.disabled = (selection.length != 1);
}

function initialize() {
    document.librarian_metadata_MetadataController = new MetadataController();
}

window.addEventListener("load", initialize);