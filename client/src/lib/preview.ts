export function previewFormError(data: Pick<FormData, 'get'>): string | undefined {
  const start = data.get('startDate');
  const end = data.get('endDate');
  if (typeof start === 'string' && typeof end === 'string' && end < start) {
    return 'Ngày kết thúc phải từ ngày bắt đầu trở đi.';
  }
  if (start === end && data.get('endTime') && String(data.get('endTime')) <= String(data.get('startTime'))) {
    return 'Giờ kết thúc phải sau giờ bắt đầu.';
  }
  if (data.get('send') === 'Lên lịch gửi' && (!data.get('scheduledDate') || !data.get('scheduledTime'))) {
    return 'Chọn ngày và giờ để kiểm tra lịch gửi.';
  }
}

export function isKnownPagePath(path: string): boolean {
  return /^\/(preview|admin)\/(dashboard|attendance|reports|permissions|help)$/.test(path) ||
    /^\/(preview|admin)\/settings(\/years)?$/.test(path) ||
    /^\/(preview|admin)\/(students|teachers|classes|events|announcements)(\/new|\/(?!new(?:\/|$))[^/]+(\/edit)?)?$/.test(path) ||
    /^\/(preview|admin)\/fees(\/[^/]+)?$/.test(path) ||
    path === '/' || path === '/login';
}
