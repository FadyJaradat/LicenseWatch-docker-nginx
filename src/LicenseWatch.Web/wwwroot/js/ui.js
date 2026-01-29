(() => {
  const root = document.documentElement;
  const storageKey = "lw-theme";
  const themeToggle = document.querySelector("[data-theme-toggle]");
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)");

  const getSystemTheme = () => (prefersDark.matches ? "dark" : "light");
  const getCookieTheme = () => {
    const match = document.cookie
      .split(";")
      .map((item) => item.trim())
      .find((item) => item.startsWith(`${storageKey}=`));
    if (!match) {
      return null;
    }
    const value = match.split("=");
    return value.length > 1 ? decodeURIComponent(value[1]) : null;
  };
  const getStoredTheme = () => localStorage.getItem(storageKey) || getCookieTheme();

  const updateToggle = (theme) => {
    if (!themeToggle) {
      return;
    }

    const icon = themeToggle.querySelector("i");
    if (icon) {
      icon.className = theme === "dark" ? "bi bi-sun" : "bi bi-moon-stars";
    }
    themeToggle.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
    themeToggle.setAttribute(
      "aria-label",
      theme === "dark" ? "Switch to light mode" : "Switch to dark mode"
    );
  };

  const applyTheme = (theme, persist) => {
    if (persist) {
      root.setAttribute("data-theme", theme);
      localStorage.setItem(storageKey, theme);
      document.cookie = `lw-theme=${theme}; path=/; max-age=31536000; samesite=lax`;
    } else {
      root.removeAttribute("data-theme");
      localStorage.removeItem(storageKey);
      document.cookie = "lw-theme=; path=/; max-age=0";
    }

    updateToggle(theme);
    document.dispatchEvent(new CustomEvent("lw:theme-change", { detail: { theme } }));
  };

  const storedTheme = getStoredTheme();
  const initialTheme = storedTheme || getSystemTheme();

  if (storedTheme) {
    root.setAttribute("data-theme", storedTheme);
  }

  updateToggle(initialTheme);

  if (themeToggle) {
    themeToggle.addEventListener("click", () => {
      const current = getStoredTheme() || getSystemTheme();
      const next = current === "dark" ? "light" : "dark";
      applyTheme(next, true);
    });
  }

  prefersDark.addEventListener("change", (event) => {
    if (getStoredTheme()) {
      return;
    }

    const theme = event.matches ? "dark" : "light";
    updateToggle(theme);
    document.dispatchEvent(new CustomEvent("lw:theme-change", { detail: { theme } }));
  });

  const toastHost = document.getElementById("lw-toast-host");

  const showToast = (message, variant) => {
    if (!toastHost) {
      return;
    }

    const iconMap = {
      success: "bi-check-circle-fill",
      danger: "bi-exclamation-triangle-fill",
      warning: "bi-exclamation-triangle-fill",
      info: "bi-info-circle-fill",
      primary: "bi-info-circle-fill",
      secondary: "bi-info-circle-fill"
    };

    const toast = document.createElement("div");
    toast.className = `lw-toast lw-toast--${variant}`;
    toast.setAttribute("role", "status");
    toast.setAttribute("aria-live", "polite");

    const icon = iconMap[variant] || iconMap.info;
    toast.innerHTML = `
      <div class="lw-toast__icon"><i class="bi ${icon}"></i></div>
      <div class="lw-toast__body">${message}</div>
      <button class="lw-toast__close" type="button" aria-label="Dismiss notification">
        <i class="bi bi-x-lg"></i>
      </button>
    `;

    toastHost.appendChild(toast);
    requestAnimationFrame(() => toast.classList.add("lw-toast--show"));

    const dismiss = () => {
      toast.classList.add("lw-toast--hide");
      setTimeout(() => toast.remove(), 200);
    };

    const closeButton = toast.querySelector(".lw-toast__close");
    if (closeButton) {
      closeButton.addEventListener("click", dismiss, { once: true });
    }

    setTimeout(dismiss, 5000);
  };

  if (toastHost) {
    document.querySelectorAll(".alert[data-toast=\"true\"]").forEach((alert) => {
      const classes = Array.from(alert.classList);
      const variant =
        classes
          .find((item) => item.startsWith("alert-") && item !== "alert")
          ?.replace("alert-", "") || "info";

      showToast(alert.innerHTML.trim(), variant);
      alert.remove();
    });
  }

  document.addEventListener("submit", (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    const method = (form.getAttribute("method") || "get").toLowerCase();
    if (method !== "post") {
      return;
    }

    const submitter = event.submitter;
    if (!(submitter instanceof HTMLButtonElement)) {
      return;
    }

    if (submitter.dataset.noLoading === "true") {
      return;
    }

    if (submitter.dataset.loadingApplied === "true") {
      return;
    }

    const label = submitter.dataset.loadingText || "Working...";
    submitter.dataset.loadingApplied = "true";
    submitter.dataset.loadingOriginal = submitter.innerHTML;
    submitter.classList.add("lw-btn-loading");
    submitter.setAttribute("aria-busy", "true");
    submitter.disabled = true;
    submitter.innerHTML = `
      <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
      <span>${label}</span>
    `;
  });

  document.querySelectorAll("form").forEach((form) => {
    const errorFields = form.querySelectorAll(
      ".field-validation-error, .input-validation-error"
    );
    if (!errorFields.length) {
      return;
    }

    if (form.querySelector("[data-valmsg-summary]")) {
      return;
    }

    if (form.querySelector(".lw-error-summary")) {
      return;
    }

    const messages = new Set();
    errorFields.forEach((field) => {
      const text = field.textContent?.trim();
      if (text) {
        messages.add(text);
      }
    });

    const summary = document.createElement("div");
    summary.className = "lw-error-summary";
    summary.setAttribute("role", "alert");

    const listItems = Array.from(messages)
      .map((text) => `<li>${text}</li>`)
      .join("");

    summary.innerHTML = `
      <div class="fw-semibold">Please fix the highlighted fields.</div>
      ${listItems ? `<ul class=\"mb-0\">${listItems}</ul>` : ""}
    `;

    form.prepend(summary);
  });

  const permissionGroupToggles = document.querySelectorAll(".lw-permission-group-toggle");
  if (permissionGroupToggles.length) {
    const updateGroupToggle = (groupIndex) => {
      const boxes = Array.from(
        document.querySelectorAll(
          `.lw-permission-checkbox[data-group-index="${groupIndex}"]`
        )
      ).filter((box) => !box.disabled);

      const toggle = document.querySelector(
        `.lw-permission-group-toggle[data-group-index="${groupIndex}"]`
      );

      if (!toggle || !boxes.length) {
        return;
      }

      toggle.checked = boxes.every((box) => box.checked);
    };

    permissionGroupToggles.forEach((toggle) => {
      toggle.addEventListener("change", () => {
        const groupIndex = toggle.dataset.groupIndex;
        if (groupIndex === undefined) {
          return;
        }

        document
          .querySelectorAll(
            `.lw-permission-checkbox[data-group-index="${groupIndex}"]`
          )
          .forEach((checkbox) => {
            if (checkbox.disabled) {
              return;
            }

            checkbox.checked = toggle.checked;
          });
      });
    });

    document.querySelectorAll(".lw-permission-checkbox").forEach((checkbox) => {
      checkbox.addEventListener("change", () => {
        const groupIndex = checkbox.dataset.groupIndex;
        if (groupIndex === undefined) {
          return;
        }

        updateGroupToggle(groupIndex);
      });
    });
  }

  document.querySelectorAll("[data-cron-preset]").forEach((button) => {
    button.addEventListener("click", () => {
      const target = document.querySelector('input[name="CronExpression"]');
      if (!target) {
        return;
      }

      const expression = button.getAttribute("data-cron-preset");
      if (expression) {
        target.value = expression;
      }
    });
  });

  const addParameterButton = document.querySelector("[data-parameter-add]");
  if (addParameterButton) {
    addParameterButton.addEventListener("click", () => {
      const list = document.querySelector(".lw-parameter-list");
      if (!list) {
        return;
      }

      const rows = list.querySelectorAll(".lw-parameter-row");
      const template = rows.length ? rows[rows.length - 1] : null;
      if (!template) {
        return;
      }

      const clone = template.cloneNode(true);
      const index = rows.length;
      clone.querySelectorAll("input").forEach((input) => {
        const name = input.getAttribute("name") || "";
        const updated = name.replace(/\[\d+\]/, `[${index}]`);
        input.setAttribute("name", updated);
        input.value = "";
      });

      list.appendChild(clone);
    });
  }

  const syncShellHeights = () => {
    const shell = document.querySelector(".lw-shell");
    if (!shell) {
      return;
    }

    const topbar = document.querySelector(".lw-topbar");
    const footer = document.querySelector(".lw-footer");
    if (topbar) {
      shell.style.setProperty("--lw-topbar-height", `${topbar.offsetHeight}px`);
    }
    if (footer) {
      shell.style.setProperty("--lw-footer-height", `${footer.offsetHeight}px`);
    }
  };

  window.addEventListener("resize", () => {
    syncShellHeights();
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", syncShellHeights, { once: true });
  } else {
    syncShellHeights();
  }

  const updateModalPadding = (isOpen) => {
    const scrollbarWidth = Math.max(
      0,
      window.innerWidth - document.documentElement.clientWidth
    );

    document.documentElement.style.setProperty(
      "--lw-scrollbar-width",
      `${scrollbarWidth}px`
    );

    const padding = isOpen ? `${scrollbarWidth}px` : "0px";
    document.body.style.paddingRight = padding;

    const topbar = document.querySelector(".lw-topbar");
    if (topbar) {
      topbar.style.paddingRight = padding;
    }

    const footer = document.querySelector(".lw-footer");
    if (footer) {
      footer.style.paddingRight = padding;
    }
  };

  document.addEventListener("show.bs.modal", (event) => {
    updateModalPadding(true);
    const modal = event.target;
    if (modal && modal instanceof HTMLElement && modal.parentElement !== document.body) {
      document.body.appendChild(modal);
    }
  });
  document.addEventListener("hidden.bs.modal", () => updateModalPadding(false));
})();
