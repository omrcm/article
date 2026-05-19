import http from 'k6/http';
import { check } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

const ttfb = new Trend('time_to_first_byte', true);
const errorRate = new Rate('errors');
const timeoutCount = new Counter('timeouts');

const ENDPOINT = __ENV.ENDPOINT || 'sync';
const BASE = __ENV.BASE || 'http://api:5000';
const REQUEST_TIMEOUT = __ENV.TIMEOUT || '5s';

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 100 },
        { duration: '60s', target: 500 },
        { duration: '60s', target: 1000 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.05'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const res = http.get(BASE + '/' + ENDPOINT, {
    timeout: REQUEST_TIMEOUT,
    tags: { endpoint: ENDPOINT },
  });

  ttfb.add(res.timings.waiting);
  errorRate.add(res.status !== 200);
  if (res.error_code === 1050 || res.status === 0) timeoutCount.add(1);

  check(res, {
    'status is 200': function (r) { return r.status === 200; },
    'response time < 500ms': function (r) { return r.timings.duration < 500; },
  });
}

function getMetric(metrics, name, field, defaultValue) {
  if (metrics[name] && metrics[name].values && metrics[name].values[field] !== undefined) {
    return metrics[name].values[field];
  }
  return defaultValue;
}

export function handleSummary(data) {
  const m = data.metrics;
  const reqs = getMetric(m, 'http_reqs', 'count', 0);
  const duration = data.state.testRunDurationMs / 1000;
  const rps = duration > 0 ? (reqs / duration).toFixed(1) : '0';
  const errRate = (getMetric(m, 'http_req_failed', 'rate', 0) * 100).toFixed(2);

  const p50 = getMetric(m, 'http_req_duration', 'med', 0).toFixed(0);
  const p95 = getMetric(m, 'http_req_duration', 'p(95)', 0).toFixed(0);
  const p99 = getMetric(m, 'http_req_duration', 'p(99)', 0).toFixed(0);
  const max = getMetric(m, 'http_req_duration', 'max', 0).toFixed(0);
  const timeouts = getMetric(m, 'timeouts', 'count', 0);

  const summary =
    '\n========================================\n' +
    '  Test Sonuçları: ' + ENDPOINT + '\n' +
    '========================================\n' +
    '  Toplam request:    ' + reqs + '\n' +
    '  Süre:              ' + duration.toFixed(0) + 's\n' +
    '  RPS:               ' + rps + '\n' +
    '  Hata oranı:        ' + errRate + '%\n' +
    '\n' +
    '  Latency (ms):\n' +
    '    p50:             ' + p50 + '\n' +
    '    p95:             ' + p95 + '\n' +
    '    p99:             ' + p99 + '\n' +
    '    max:             ' + max + '\n' +
    '\n' +
    '  Timeout sayısı:    ' + timeouts + '\n' +
    '========================================\n';

  const result = {};
  result['stdout'] = summary;
  result['/results/summary-' + ENDPOINT + '.json'] = JSON.stringify(data, null, 2);
  return result;
}
