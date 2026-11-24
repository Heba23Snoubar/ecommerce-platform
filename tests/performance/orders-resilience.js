import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:8088';
const TOKEN = __ENV.K6_TOKEN || '';

export const options = {
    scenarios: {
        resilience_test: {
            executor: 'constant-vus',
            vus: 10,
            duration: '90s',
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
};

export default function () {
    const response = http.get(
        `${BASE_URL}/api/v1/orders?page=1&pageSize=50`,
        {
            headers: {
                Authorization: `Bearer ${TOKEN}`,
            },
            timeout: '10s',
        }
    );

    const servedBy =
        response.headers['X-Served-By'] ||
        response.headers['x-served-by'] ||
        'MISSING';

    check(response, {
        'orders endpoint returns 200': (r) => r.status === 200,
        'served-by header exists': (r) =>
            r.status !== 200 ||
            Boolean(r.headers['X-Served-By'] || r.headers['x-served-by']),
    });

    console.log(
        `T3_RESULT,${Date.now()},${__VU},${__ITER},${response.status},${servedBy},${response.timings.duration}`
    );
}
