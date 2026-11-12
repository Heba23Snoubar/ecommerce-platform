import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5097';
const PRODUCT_ID =
  __ENV.PRODUCT_ID || '01a1a8fd-ccd4-74cb-b9d1-524795bd1a20';

const redisProductHit = new Trend('redis_product_hit_duration');

export const options = {
  vus: 10,
  duration: '30s',
  summaryTrendStats: ['min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    checks: ['rate>0.95'],
    redis_product_hit_duration: ['p(95)<500'],
  },
};

export default function () {
  const response = http.get(
    `${BASE_URL}/api/v1/products/${PRODUCT_ID}`
  );

  redisProductHit.add(response.timings.duration);

  check(response, {
    'product returns 200': (r) => r.status === 200,
    'product has body': (r) => r.body && r.body.length > 0,
  });
}