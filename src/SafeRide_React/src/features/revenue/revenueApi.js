export function getRevenuePath(from, to) {
  const params = new URLSearchParams();
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  const query = params.toString();
  return `/admin/revenue${query ? `?${query}` : ''}`;
}

export function mapRevenue(response) {
  return { ...response, timeline: response.timeline ?? [], services: response.services ?? [] };
}
