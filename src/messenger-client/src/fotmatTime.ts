const clock = new Intl.DateTimeFormat('ru-RU', {
    hour: "2-digit",
    minute: "2-digit",
});

export function formatTime(value: string): string {
    return clock.format(new Date(value));
}