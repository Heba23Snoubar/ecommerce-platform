import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5097';
const TOKEN = __ENV.K6_TOKEN || '';

export const options = {
  vus: 10,
  duration: '30s',
  summaryTrendStats: ['min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    checks: ['rate>0.95'],
  },
};

export default function () {
  const response = http.get(
    `${BASE_URL}/api/v1/orders?page=1&pageSize=50`,
    {
      headers: {
        Authorization: `Bearer ${TOKEN}`,
      },
    }
  );

  check(response, {
    'orders list returns 200': (r) => r.status === 200,
  });

  sleep(0.1);
}