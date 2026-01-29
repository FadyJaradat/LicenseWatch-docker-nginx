// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("click", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return;
  }

  const button = target.closest("[data-export-button]");
  if (!button) {
    return;
  }

  button.setAttribute("aria-busy", "true");
  button.classList.add("disabled");
  if (button instanceof HTMLButtonElement) {
    button.disabled = true;
  }

  const label = button.querySelector(".export-label");
  if (label) {
    label.textContent = "Preparing...";
  }

  const spinner = button.querySelector(".export-spinner");
  if (spinner) {
    spinner.classList.remove("d-none");
  }
});
