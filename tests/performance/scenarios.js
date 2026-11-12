import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5097';
const TOKEN = __ENV.K6_TOKEN || '';

const headers = TOKEN
  ? { Authorization: `Bearer ${TOKEN}` }
  : {};

export const options = {
  summaryTrendStats: ['min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  scenarios: {
    S1_product_list: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'productList' },
    S2_product_details: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'productDetails' },
    S3_etag_304: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'etag304' },
    S4_different_queries: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'differentQueries' },
    S5_product_update: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'productUpdate' },
    S6_missing_product: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'missingProduct' },
    S7_mixed_workload: { executor: 'constant-vus', vus: 1, duration: '10s', exec: 'mixedWorkload' },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<1000'],
    checks: ['rate>0.95'],
  },
};

function request(method, url, body = null, extraHeaders = {}) {
  return http.request(method, url, body, {
    headers: {
      ...headers,
      ...extraHeaders,
    },
  });
}

// ==========================================
// 1. التجهيز التلقائي (Auto-Setup)
// ==========================================
export function setup() {
  const listUrl = `${BASE_URL}/api/v1/products?pageNumber=1&pageSize=10`;
  const response = request('GET', listUrl);

  check(response, {
    'setup products fetched successfully': (r) => r.status === 200,
  });

  if (response.status !== 200) {
    throw new Error(`Setup failed: Unable to fetch products list (Status: ${response.status})`);
  }

  const data = JSON.parse(response.body);
  const items = Array.isArray(data) ? data : (data.items || data.products || []);

  if (items.length === 0) {
    throw new Error('Setup failed: No products found in the database. Please run the seed first.');
  }

  const product = items[0];
  const updateProduct = items.length > 1 ? items[1] : product;

  console.log(`[Auto-Setup] Selected Product ID: ${product.id}`);
  console.log(`[Auto-Setup] Selected Update Product ID: ${updateProduct.id}`);

  return {
    productId: product.id,
    updateProductId: updateProduct.id,
    originalPrice: product.price,
  };
}

// ==========================================
// 2. سيناريوهات الاختبار الـ 7
// ==========================================

export function productList() {
  const listUrl = `${BASE_URL}/api/v1/products?pageNumber=1&pageSize=50`;
  const response = request('GET', listUrl);

  check(response, {
    'S1 list returns 200': (r) => r.status === 200,
    'S1 list has ETag': (r) => Boolean(r.headers.Etag),
  });

  sleep(1);
}

export function productDetails(data) {
  const productUrl = `${BASE_URL}/api/v1/products/${data.productId}`;
  const response = request('GET', productUrl);

  check(response, {
    'S2 details return 200': (r) => r.status === 200,
    'S2 details have body': (r) => r.body.length > 0,
    'S2 details have ETag': (r) => Boolean(r.headers.Etag),
  });

  sleep(1);
}

export function etag304(data) {
  const productUrl = `${BASE_URL}/api/v1/products/${data.productId}`;
  const first = request('GET', productUrl);
  const etag = first.headers.Etag;

  if (!etag) {
    check(first, {
      'S3 initial response has ETag': () => false,
    });
    return;
  }

  const second = request('GET', productUrl, null, {
    'If-None-Match': etag,
  });

  check(second, {
    'S3 matching ETag returns 304': (r) => r.status === 304,
    'S3 304 has empty body': (r) => (r.body || '').length === 0,
  });

  sleep(1);
}

export function differentQueries() {
  const pageSize = Math.random() > 0.5 ? 2 : 3;
  const response = request(
    'GET',
    `${BASE_URL}/api/v1/products?pageNumber=1&pageSize=${pageSize}`
  );

  check(response, {
    'S4 query returns 200': (r) => r.status === 200,
    'S4 query has ETag': (r) => Boolean(r.headers.Etag),
  });

  sleep(1);
}

export function productUpdate(data) {
  const updateUrl = `${BASE_URL}/api/v1/products/${data.updateProductId}`;
  const updatedPrice = Number(data.originalPrice) + 1;

  const update = request(
    'PATCH',
    `${updateUrl}/price`,
    JSON.stringify({ price: updatedPrice }),
    { 'Content-Type': 'application/json' }
  );

  check(update, {
    'S5 price update returns 204': (r) => r.status === 204,
  });

  const afterUpdate = request('GET', updateUrl);

  check(afterUpdate, {
    'S5 updated price is immediately visible': (r) =>
      r.status === 200 &&
      Number(JSON.parse(r.body).price) === updatedPrice,
  });

  const restore = request(
    'PATCH',
    `${updateUrl}/price`,
    JSON.stringify({ price: data.originalPrice }),
    { 'Content-Type': 'application/json' }
  );

  check(restore, {
    'S5 price restore returns 204': (r) => r.status === 204,
  });

  sleep(1);
}

export function missingProduct() {
  const unknownId = '00000000-0000-0000-0000-000000000000';

  const response = http.get(
    `${BASE_URL}/api/v1/products/${unknownId}`,
    {
      headers,
      responseCallback: http.expectedStatuses(404),
    }
  );

  check(response, {
    'S6 unknown product returns 404': (r) => r.status === 404,
    'S6 error has no ETag': (r) => !r.headers.Etag,
  });

  sleep(1);
}

export function mixedWorkload(data) {
  const listUrl = `${BASE_URL}/api/v1/products?pageNumber=1&pageSize=50`;
  const productUrl = `${BASE_URL}/api/v1/products/${data.productId}`;

  const list = request('GET', listUrl);

  check(list, {
    'S7 list returns 200': (r) => r.status === 200,
  });

  const details = request('GET', productUrl);

  check(details, {
    'S7 details return 200': (r) => r.status === 200,
  });

  const etag = details.headers.Etag;

  if (etag) {
    const cached = request('GET', productUrl, null, {
      'If-None-Match': etag,
    });

    check(cached, {
      'S7 cached details return 304': (r) => r.status === 304,
    });
  }

  sleep(1);
}