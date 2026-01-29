(() => {
  const payloadElement = document.getElementById("dashboard-data");
  if (!payloadElement) {
    return;
  }

  let payload = {};
  const rawPayload = payloadElement.getAttribute("data-payload");
  if (rawPayload) {
    try {
      payload = JSON.parse(rawPayload);
    } catch {
      payload = {};
    }
  }

  if (typeof Chart === "undefined") {
    return;
  }

  const chartInstances = [];

  const getPalette = () => {
    const styles = getComputedStyle(document.documentElement);
    const value = (name, fallback) => {
      const current = styles.getPropertyValue(name).trim();
      return current || fallback;
    };

    return {
      primary: value("--lw-primary", "#1f7ae0"),
      primarySoft: value("--lw-primary-soft", "rgba(31, 122, 224, 0.2)"),
      success: value("--lw-success", "#16a34a"),
      warning: value("--lw-warning", "#f59e0b"),
      danger: value("--lw-danger", "#ef4444"),
      info: value("--lw-info", "#0ea5e9"),
      infoSoft: value("--lw-info-soft", "rgba(14, 165, 233, 0.16)"),
      muted: value("--lw-muted", "#64748b"),
      text: value("--lw-text", "#0f172a"),
      surface: value("--lw-surface", "#ffffff"),
      grid: value("--lw-chart-grid", "rgba(15, 23, 42, 0.08)"),
      font: value("--lw-font-body", "Manrope, sans-serif")
    };
  };

  const setChartDefaults = (palette) => {
    Chart.defaults.font.family = palette.font;
    Chart.defaults.color = palette.muted;
    Chart.defaults.plugins.tooltip.backgroundColor = palette.surface;
    Chart.defaults.plugins.tooltip.titleColor = palette.text;
    Chart.defaults.plugins.tooltip.bodyColor = palette.muted;
    Chart.defaults.plugins.tooltip.borderColor = palette.grid;
    Chart.defaults.plugins.tooltip.borderWidth = 1;
  };

  const markLoaded = (canvas) => {
    const wrapper = canvas.closest(".lw-chart");
    if (wrapper) {
      wrapper.classList.add("is-loaded");
    }
  };

  const buildBarChart = (id, labels, values, links, color, borderColor) => {
    const canvas = document.getElementById(id);
    if (!canvas || !labels || labels.length === 0) {
      return;
    }

    const palette = getPalette();

    const chart = new Chart(canvas, {
      type: "bar",
      data: {
        labels,
        datasets: [
          {
            data: values,
            backgroundColor: color,
            borderColor: borderColor,
            borderWidth: 1,
            borderRadius: 6
          }
        ]
      },
      options: {
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (context) => `${context.parsed.y} licenses`
            }
          }
        },
        onClick: (_event, elements) => {
          if (!elements.length || !links) {
            return;
          }
          const index = elements[0].index;
          const target = links[index];
          if (target) {
            window.location.href = target;
          }
        },
        scales: {
          x: {
            grid: { color: palette.grid },
            ticks: { color: palette.muted }
          },
          y: {
            beginAtZero: true,
            grid: { color: palette.grid },
            ticks: { color: palette.muted }
          }
        }
      }
    });

    chartInstances.push(chart);
    markLoaded(canvas);
  };

  const buildStatusChart = (labels, values, links) => {
    const canvas = document.getElementById("statusChart");
    if (!canvas || !labels || labels.length === 0) {
      return;
    }

    const palette = getPalette();

    const colors = labels.map((label) => {
      switch ((label || "").toLowerCase()) {
        case "good":
          return palette.success;
        case "warning":
          return palette.warning;
        case "critical":
          return palette.danger;
        case "expired":
          return palette.danger;
        default:
          return palette.info;
      }
    });

    const chart = new Chart(canvas, {
      type: "doughnut",
      data: {
        labels,
        datasets: [
          {
            data: values,
            backgroundColor: colors
          }
        ]
      },
      options: {
        cutout: "68%",
        plugins: {
          legend: {
            position: "bottom",
            labels: { color: palette.muted },
            onClick: (_event, legendItem) => {
              if (!links) {
                return;
              }
              const target = links[legendItem.index];
              if (target) {
                window.location.href = target;
              }
            }
          }
        }
      }
    });

    chartInstances.push(chart);
    markLoaded(canvas);
  };

  const animateCounters = () => {
    const prefersReduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    document.querySelectorAll("[data-counter]").forEach((element) => {
      const target = Number.parseInt(element.getAttribute("data-counter") || "0", 10);
      if (!Number.isFinite(target)) {
        return;
      }

      if (prefersReduced) {
        element.textContent = new Intl.NumberFormat().format(target);
        return;
      }

      const duration = 700;
      const start = performance.now();
      const formatter = new Intl.NumberFormat();
      const tick = (now) => {
        const progress = Math.min((now - start) / duration, 1);
        element.textContent = formatter.format(Math.floor(progress * target));
        if (progress < 1) {
          requestAnimationFrame(tick);
        }
      };
      requestAnimationFrame(tick);
    });
  };

  const initCharts = () => {
    const palette = getPalette();
    setChartDefaults(palette);
    buildBarChart("trendChart", payload.trend?.labels, payload.trend?.values, payload.trend?.links, palette.primarySoft, palette.primary);
    buildStatusChart(payload.status?.labels, payload.status?.values, payload.status?.links);
    buildBarChart("vendorChart", payload.vendors?.labels, payload.vendors?.values, payload.vendors?.links, palette.infoSoft, palette.info);
  };

  const resetCharts = () => {
    chartInstances.splice(0).forEach((chart) => chart.destroy());
    document.querySelectorAll(".lw-chart").forEach((chart) => chart.classList.remove("is-loaded"));
  };

  initCharts();
  animateCounters();

  document.addEventListener("lw:theme-change", () => {
    resetCharts();
    initCharts();
  });
})();
