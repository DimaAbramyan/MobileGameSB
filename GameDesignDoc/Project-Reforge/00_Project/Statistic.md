# Weapon Progress

## Total Progress

```dataviewjs
const page = dv.page("Weapon-Checklist");
const tasks = page.file.tasks;

const total = tasks.length;
const completed = tasks.where(t => t.completed).length;
const percent = total ? Math.round(completed / total * 100) : 0;

dv.paragraph(
    `<progress value="${completed}" max="${total}"></progress> ` +
    `${completed}/${total} — ${percent}%`
);
```

---

## Weapon Category Progress

```dataviewjs
const page = dv.page("Weapon-Checklist");
const tasks = page.file.tasks;

const categories = [
    "Mechanic",
    "Visual",
    "Sound",
    "Economy",
    "Balance",
    "Polish"
];

for (const category of categories) {
    const categoryTasks = tasks.where(t =>
        t.text.trim().toLowerCase() === category.toLowerCase()
    );

    const total = categoryTasks.length;
    const completed = categoryTasks.where(t => t.completed).length;
    const percent = total ? Math.round(completed / total * 100) : 0;

    dv.paragraph(
        `**${category}**  ` +
        `<progress value="${completed}" max="${total}"></progress> ` +
        `${completed}/${total} — ${percent}%`
    );
}
```

---



