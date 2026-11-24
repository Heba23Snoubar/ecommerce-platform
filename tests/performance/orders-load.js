import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:8088';
const TOKEN = __ENV.K6_TOKEN || '';

export const options = {
    scenarios: {
        orders_load: {
            executor: 'shared-iterations',
            vus: 10,
            iterations: 500,
            maxDuration: '5m',
        },
    },

    summaryTrendStats: [
        'min',
        'med',
        'p(90)',
        'p(95)',
        'p(99)',
        'max',
    ],

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

    const servedBy =
        response.headers['X-Served-By'] ||
        response.headers['x-served-by'] ||
        'MISSING';

    check(response, {
        'orders list returns 200': (r) => r.status === 200,
        'X-Served-By header exists': () => servedBy !== 'MISSING',
    });

    // T2 result: virtual user, iteration, status, instance, response time
    console.log(
        `T2_RESULT,${__VU},${__ITER},${response.status},${servedBy},${response.timings.duration}`
    );
}
