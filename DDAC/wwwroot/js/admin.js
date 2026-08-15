

(function () {
  "use strict";

  // ---------- Toast ----------
  function showToast(message) {
    if (!message) return;
    var existing = document.querySelector(".admin-toast");
    if (existing) existing.remove();

    var toast = document.createElement("div");
    toast.className = "admin-toast";

    var dot = document.createElement("span");
    dot.className = "admin-toast-dot";

    var text = document.createElement("span");
    text.textContent = message;

    toast.appendChild(dot);
    toast.appendChild(text);
    document.body.appendChild(toast);

    setTimeout(function () {
      toast.classList.add("admin-toast-out");
      setTimeout(function () { toast.remove(); }, 250);
    }, 3200);
  }


  document.addEventListener("DOMContentLoaded", function () {
    var flash = document.body.getAttribute("data-flash");
    if (flash) showToast(flash);
  });

  // ---------- Search filter ----------
  
  document.addEventListener("input", function (e) {
    var input = e.target.closest("[data-filter-input]");
    if (!input) return;
    applyFilters(input.closest("[data-filter-scope]") || document);
  });

  // ---------- Status / role tabs ----------
  
  document.addEventListener("click", function (e) {
    var tab = e.target.closest("[data-filter-tab]");
    if (!tab) return;
    var scope = tab.closest("[data-filter-scope]") || document;
    scope.querySelectorAll("[data-filter-tab][data-filter-group='" + tab.getAttribute("data-filter-group") + "']")
      .forEach(function (btn) { btn.classList.remove("active"); });
    tab.classList.add("active");
    applyFilters(scope);
  });

  function applyFilters(scope) {
    var searchInput = scope.querySelector("[data-filter-input]");
    var query = searchInput ? searchInput.value.trim().toLowerCase() : "";

    // Group active tab values 
    var activeByGroup = {};
    scope.querySelectorAll("[data-filter-tab].active").forEach(function (btn) {
      activeByGroup[btn.getAttribute("data-filter-group")] = btn.getAttribute("data-filter-value");
    });

    var rows = scope.querySelectorAll("[data-search]");
    var visibleCount = 0;

    rows.forEach(function (row) {
      var matchesSearch = !query || row.getAttribute("data-search").toLowerCase().indexOf(query) !== -1;

      var matchesTabs = true;
      Object.keys(activeByGroup).forEach(function (group) {
        var wanted = activeByGroup[group];
        if (wanted === "All") return;
        var rowValue = row.getAttribute("data-" + group);
        if (rowValue !== wanted) matchesTabs = false;
      });

      var visible = matchesSearch && matchesTabs;
      row.style.display = visible ? "" : "none";
      if (visible) visibleCount++;
    });

    var countEl = scope.querySelector("[data-filter-count]");
    if (countEl) countEl.textContent = visibleCount;

    var emptyEl = scope.querySelector("[data-filter-empty]");
    if (emptyEl) emptyEl.style.display = visibleCount === 0 ? "" : "none";

    var tableEl = scope.querySelector("[data-filter-table]");
    if (tableEl) tableEl.style.display = visibleCount === 0 ? "none" : "";
  }

  // ---------- Row detail drawer ----------

  document.addEventListener("click", function (e) {
    if (e.target.closest("form")) return;

    var row = e.target.closest("[data-drawer-row]");
    if (row) {
      openDrawer(row);
      return;
    }

    if (e.target.closest("[data-drawer-close]") || e.target.classList.contains("admin-drawer-overlay")) {
      closeDrawer();
    }
  });

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") closeDrawer();
  });

  function openDrawer(row) {
    var overlay = document.querySelector(".admin-drawer-overlay");
    var drawer = document.querySelector(".admin-drawer");
    if (!overlay || !drawer) return;

    var fieldSpec = (row.getAttribute("data-drawer-fields") || "").split(",");
    var body = drawer.querySelector(".admin-drawer-body");
    body.innerHTML = "";

    fieldSpec.forEach(function (pair) {
      var parts = pair.split(":");
      var label = parts[0];
      var attr = parts[1];
      if (!label || !attr) return;
      var value = row.getAttribute("data-" + attr) || "-";

      var field = document.createElement("div");
      field.className = "admin-drawer-field";

      var labelEl = document.createElement("div");
      labelEl.className = "admin-drawer-field-label";
      labelEl.textContent = label;

      var valueEl = document.createElement("div");
      valueEl.className = "admin-drawer-field-value";
      valueEl.textContent = value;

      field.appendChild(labelEl);
      field.appendChild(valueEl);
      body.appendChild(field);
    });

    var titleEl = drawer.querySelector(".admin-drawer-title");
    var subtitleEl = drawer.querySelector(".admin-drawer-subtitle");
    if (titleEl) titleEl.textContent = row.getAttribute("data-drawer-title") || "Details";
    if (subtitleEl) subtitleEl.textContent = row.getAttribute("data-drawer-subtitle") || "";


    var footer = drawer.querySelector(".admin-drawer-footer");
    if (footer) {
      footer.innerHTML = "";

      var deleteAction = row.getAttribute("data-drawer-delete-action");
      if (deleteAction) {
        var form = document.createElement("form");
        form.method = "post";
        form.action = deleteAction;

        var idInput = document.createElement("input");
        idInput.type = "hidden";
        idInput.name = "id";
        idInput.value = row.getAttribute("data-drawer-delete-id") || "";
        form.appendChild(idInput);

        var returnInput = document.createElement("input");
        returnInput.type = "hidden";
        returnInput.name = "returnUrl";
        returnInput.value = window.location.pathname + window.location.search;
        form.appendChild(returnInput);

        var confirmMsg = row.getAttribute("data-drawer-delete-confirm");
        if (confirmMsg) {
          form.addEventListener("submit", function (e) {
            if (!confirm(confirmMsg)) e.preventDefault();
          });
        }

        var deleteBtn = document.createElement("button");
        deleteBtn.type = "submit";
        deleteBtn.className = "btn btn-danger";
        deleteBtn.textContent = row.getAttribute("data-drawer-delete-label") || "Remove";
        form.appendChild(deleteBtn);

        footer.appendChild(form);
      }

      var closeBtn = document.createElement("button");
      closeBtn.type = "button";
      closeBtn.className = "btn btn-secondary";
      closeBtn.setAttribute("data-drawer-close", "");
      closeBtn.textContent = "Close";
      footer.appendChild(closeBtn);
    }

    overlay.classList.add("open");
    drawer.classList.add("open");
    document.body.style.overflow = "hidden";
  }

  function closeDrawer() {
    var overlay = document.querySelector(".admin-drawer-overlay");
    var drawer = document.querySelector(".admin-drawer");
    if (overlay) overlay.classList.remove("open");
    if (drawer) drawer.classList.remove("open");
    document.body.style.overflow = "";
  }

  // ---------- Top search: jump between Admin functions ----------

  document.addEventListener("DOMContentLoaded", function () {
    var input = document.querySelector("[data-admin-search-input]");
    var results = document.querySelector("[data-admin-search-results]");
    if (!input || !results) return;

    var links = Array.prototype.slice.call(document.querySelectorAll(".admin-nav-link")).map(function (a) {
      return { text: a.textContent.trim(), href: a.getAttribute("href") };
    });

    function render(query) {
      var q = query.trim().toLowerCase();
      var matches = q ? links.filter(function (l) { return l.text.toLowerCase().indexOf(q) !== -1; }) : links;

      results.innerHTML = "";
      if (matches.length === 0) {
        results.innerHTML = '<div class="admin-search-empty">No matching function</div>';
      } else {
        matches.forEach(function (m, i) {
          var a = document.createElement("a");
          a.className = "admin-search-result" + (i === 0 ? " active" : "");
          a.href = m.href;
          a.textContent = m.text;
          results.appendChild(a);
        });
      }
      results.classList.add("open");
    }

    input.addEventListener("focus", function () { render(input.value); });
    input.addEventListener("input", function () { render(input.value); });

    input.addEventListener("keydown", function (e) {
      if (e.key === "Enter") {
        var top = results.querySelector(".admin-search-result");
        if (top) window.location.href = top.getAttribute("href");
      }
    });

    document.addEventListener("click", function (e) {
      if (!e.target.closest(".admin-search")) {
        results.classList.remove("open");
      }
    });
  });

  // ---------- Screen reader hints ----------
 
  document.addEventListener("DOMContentLoaded", function () {
    if (document.body.getAttribute("data-screen-reader-hints") !== "true") return;

    document.querySelectorAll("table.table tbody tr").forEach(function (row) {
      var firstCell = row.querySelector("td");
      var subject = firstCell ? firstCell.textContent.trim() : "";
      if (!subject) return;

      row.querySelectorAll(".btn").forEach(function (btn) {
        var action = btn.textContent.trim();
        if (!action) return;
        btn.setAttribute("aria-label", action + " - " + subject);
      });

      if (row.hasAttribute("data-drawer-row")) {
        row.setAttribute("aria-label", "View details for " + subject);
      }
    });

    document.querySelectorAll(".admin-nav-link").forEach(function (link) {
      if (link.classList.contains("active")) {
        link.setAttribute("aria-current", "page");
      }
    });
  });
})();
