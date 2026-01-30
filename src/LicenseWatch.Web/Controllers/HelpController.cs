using LicenseWatch.Web.Models.Help;
using Microsoft.AspNetCore.Mvc;

namespace LicenseWatch.Web.Controllers;

[Route("help")]
public class HelpController : Controller
{
    [HttpGet("new-features")]
    public IActionResult NewFeatures()
    {
        var vm = new ReleaseNotesViewModel
        {
            Versions = new List<ReleaseNotesVersion>
            {
                new()
                {
                    Version = "v4.3.4",
                    Title = "Dependency refresh + stability updates",
                    DateLabel = "January 2026",
                    IsCurrent = true,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Upgraded Hangfire.Core to 1.8.22 for the latest fixes.",
                                "Updated CsvHelper to 33.1.0 and Cronos to 0.11.1.",
                                "Upgraded test tooling (Microsoft.NET.Test.Sdk + xUnit runner) to latest."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Dependency refresh across production and test packages to reduce known issues."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.3.3",
                    Title = "Governance hardening + modal stability",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Global modal stability: fixed-centered dialogs with zero layout jump on open/close.",
                                "Audit & system logs now display the actor for each event (with impersonation indicators when applicable).",
                                "Role-based redaction for sensitive log payload fields.",
                                "Correlation IDs linking user actions → jobs → audit/log entries.",
                                "Sidebar small-resolution support: internal scrolling keeps all nav items reachable.",
                                "Users page frame height normalization + safe user deletion workflow (audited)."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Delete Role and System Log dialogs now stay centered and stable while scrolling."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.3.2",
                    Title = "Role permissions schema guard",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Startup migration guard now ensures RolePermissions schema exists before admin renders."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Resolved admin load failures when RolePermissions table is missing in existing App DBs."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.3.1",
                    Title = "Migration guard + admin stability",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Startup migration guard ensures license threshold columns exist before admin loads.",
                                "Admin pages recover gracefully after schema updates."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Optimization and dashboard views no longer fail when schema drift is detected."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Resolved admin error caused by missing license threshold columns after deployment."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.3",
                    Title = "Audit transparency + user lifecycle governance",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Fixed modal positioning: Delete Role and System Log dialogs now stay centered and stable.",
                                "Added full audit transparency: logs now show which user performed each action.",
                                "Added ability to delete users with safety checks and audit logging.",
                                "Fixed Users page layout/frame height consistency.",
                                "Improved audit/system log clarity for enterprise governance.",
                                "Version synchronization enforced across UI, emails, and reports."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "System logs now include the actor for job/email/audit entries."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Delete Role modal now stays centered while scrolling.",
                                "System log detail dialog no longer shifts with scroll."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.2",
                    Title = "Dashboard UX + automation transparency + governance controls",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Dashboard layout normalization and stacked KPI sections.",
                                "Simplified Immediate Actions list (no sub-tabs).",
                                "Automatic compliance evaluation on license changes.",
                                "Configurable severity thresholds (system-wide + per-license).",
                                "Compliance transparency (toasts + timestamps).",
                                "Audit visibility fixes and CSV export.",
                                "Audit retention policies.",
                                "Safe deletion rules for custom roles.",
                                "Advanced email notifications and scheduled reports.",
                                "Customer branding (logo + name).",
                                "Permanent version synchronization across UI, emails, and reports."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Immediate actions list now prioritizes critical items first with a single review CTA.",
                                "Dashboard cards normalized for consistent width, height, and spacing across rows."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Severity classification now consistently reflects the configured thresholds across dashboard and lists.",
                                "Audit default filters adjusted to prevent silent empty results."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.1",
                    Title = "Cleaner dashboard, consistent layout, safer automation, reliable auditing",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "Immediate actions unified into a single list with severity badges.",
                                "System audit stream now surfaces in System logs for reliable visibility."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Dashboard header simplified by removing redundant Run jobs and Reports buttons.",
                                "Renewal timeline, optimization opportunities, portfolio health, and vendor exposure cards normalized to equal width and height.",
                                "License details view now matches the full app frame for consistent vertical rhythm."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Compliance evaluation now runs automatically after license updates to prevent stale posture.",
                                "Audit list defaults corrected so recent entries always appear when filters are clear."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v4.0",
                    Title = "Launch readiness hardening",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "New features",
                            Items = new List<string>
                            {
                                "RBAC permission matrix and role safeguards.",
                                "Jobs scheduler control plane with safe auditing."
                            }
                        },
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Executive dashboard hierarchy with calmer layout.",
                                "Dark mode contrast fixes for tables and cards.",
                                "Operational health checks for core dependencies."
                            }
                        },
                        new()
                        {
                            Title = "Fixes",
                            Items = new List<string>
                            {
                                "Improved error messaging and audit detail views.",
                                "Email diagnostics and safer TLS controls."
                            }
                        }
                    }
                },
                new()
                {
                    Version = "v3.9",
                    Title = "Interaction polish + dark mode",
                    DateLabel = "January 2026",
                    IsCurrent = false,
                    Sections = new List<ReleaseNotesSection>
                    {
                        new()
                        {
                            Title = "Improvements",
                            Items = new List<string>
                            {
                                "Premium UI kit rollout and aligned admin layout.",
                                "Micro-interactions, toasts, and loading states."
                            }
                        }
                    }
                }
            }
        };

        return View(vm);
    }
}
