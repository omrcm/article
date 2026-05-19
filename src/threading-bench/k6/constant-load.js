import http from 'k6/http';
import { check } from 'k6';

const ENDPOINT = __ENV.ENDPOINT || 'sync';
const BASE = __ENV.BASE || 'http://api:5000';
const VUS = parseInt(__ENV.VUS || '500');
const DURATION = __ENV.DURATION || '2m';

export const options = {
  vus: VUS,
  duration: DURATION,
  thresholds: { http_req_duration: ['p(95)<500'] },
};

export default function () {
  const res = http.get(`${BASE}/${ENDPOINT}`, { timeout: '5s' });
  check(res, { 'status 200': (r) => r.status === 200 });
}
