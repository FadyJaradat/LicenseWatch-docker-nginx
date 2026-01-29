# UI/UX Acceptance Checklist

Use this checklist for every UI change. If any item is "No", the page is not ship-ready.

## 1) Visual Hierarchy
- [ ] Page uses the shared PageHeader with Title + Subtitle.
- [ ] There is a clear primary action visible without scrolling.
- [ ] Content is grouped into SectionCards with clear section titles.
- [ ] No page is just a plain table or wall of text.

## 2) Density & Spacing
- [ ] Standard vertical spacing exists between sections (>= 24px).
- [ ] No unbroken blocks of text longer than 4 lines.
- [ ] Tables have zebra or hover states, and are wrapped for mobile.
- [ ] Critical metrics are elevated (KPI cards or highlights).

## 3) Interaction & Feedback
- [ ] Every form provides inline validation or clear error messaging.
- [ ] Success and failure states are shown as toast/alert banners.
- [ ] Destructive actions require confirmation (modal or prompt).
- [ ] Loading state exists for charts or data-heavy sections.

## 4) Decision Support Standard (Management-grade)
- [ ] Dashboard shows: KPI summary, Needs Attention list, Trends chart, Recent activity.
- [ ] Each KPI or insight has a drilldown path to a detail page.
- [ ] Priority items include a CTA to resolve or review.

## 5) Empty States
- [ ] Every list/table has a premium empty state with guidance.
- [ ] Empty states include at least one suggested next action.

## 6) Accessibility
- [ ] Keyboard navigation works for nav, tabs, and buttons.
- [ ] Focus rings are visible on interactive elements.
- [ ] Color contrast is acceptable for text and badges.
- [ ] Icon-only buttons have aria-labels.

## 7) Consistency
- [ ] Shared components are used (PageHeader, SectionCard, KPI card).
- [ ] Buttons, badges, and cards use the LW theme classes.
- [ ] Layout spacing and typography are consistent across pages.
