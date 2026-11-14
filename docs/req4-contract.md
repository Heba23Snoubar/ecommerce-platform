# عقد المتطلب الرابع: المعالجة غير المتزامنة والتحديثات اللحظية

هذه الوثيقة هي المرجع المشترك بين أعضاء الفريق الأربعة. أي تغيير في الأسماء أو الأشكال أدناه يُعدَّل هنا أولاً عبر PR، ثم يُبلَّغ به الفريق.

**المسؤوليات:** أ = Publisher وRabbitMQ · ب = Worker والبريد · ج = الـ real-time في الخلفية · د = الواجهة والتوثيق

---

## 1. نظرة عامة على التدفق

```
الزبون ── POST /api/v1/orders ──► AppService ── يحفظ الطلب في قاعدة البيانات
                                      │
                                      ├─► ينشر order.created إلى RabbitMQ ──► Worker ──► بريد حقيقي للزبون
                                      │
                                      └─► يبثّ order.created على private-admin-orders ──► لوحة المسؤول

المسؤول ── PATCH /api/v1/orders/{id}/status ──► AppService ── يحدّث الحالة
                                      │
                                      └─► يبثّ order.status-changed على private-order.{id} ──► صفحة الزبون
```

قاعدتان لا تُكسران:

1. **الرد على الزبون فوري:** لا ينتظر التطبيق إرسال البريد.
2. **الـ real-time لا يعتمد على الـ Worker:** يُبَثّ الحدث من التطبيق مباشرة بعد الحفظ، فيظهر الطلب في لوحة المسؤول حتى لو كان الـ Worker متوقفاً.

---

## 2. RabbitMQ

| العنصر | الاسم | النوع / الخصائص |
|---|---|---|
| Exchange رئيسي | `orders.events` | direct، durable |
| Routing key | `order.created` | |
| الطابور الرئيسي | `orders.email` | durable، مربوط بـ `orders.events` بالمفتاح `order.created` |
| Dead Letter Exchange | `orders.dlx` | direct، durable |
| طابور الرسائل الميتة (DLQ) | `orders.email.dlq` | durable، مربوط بـ `orders.dlx` بالمفتاح `orders.email.dlq` |

وسائط الطابور الرئيسي: `x-dead-letter-exchange = orders.dlx` و`x-dead-letter-routing-key = orders.email.dlq`.

**قواعد النشر والاستهلاك:**

- الرسائل `persistent` وتُنشر مع publisher confirms.
- الـ Worker يستعمل `prefetch = 1`، و**ack يدوي بعد نجاح إرسال البريد فقط**.
- الفشل: إعادة المحاولة عدة مرات، ثم `reject` بدون `requeue` لتنتقل الرسالة إلى `orders.email.dlq`.
- **مفتاح الـ idempotency هو `orderId`:** بريد واحد فقط لكل طلب. إن أعيد تسليم رسالة طلب أُرسل بريده سابقاً فتُعمل `ack` عليها دون إرسال ثانٍ.
- تعديل وسائط طابور موجود يتطلب حذفه من واجهة الإدارة أولاً (وإلا خطأ `PRECONDITION_FAILED`).

**العناوين (مهم):**

| من أين | العنوان |
|---|---|
| التطبيق (يعمل على الجهاز عبر `dotnet run`) | `localhost:5672` |
| الـ Worker داخل Docker Compose | `rabbitmq:5672` (يُضبط `RABBITMQ_HOST=rabbitmq` في خدمته داخل compose) |
| واجهة الإدارة | `http://localhost:15672` |

لا يُستعمل المستخدم `guest`. المستخدم من `RABBITMQ_USER` و`RABBITMQ_PASSWORD`.

---

## 3. رسالة `order.created` (RabbitMQ)

الجسم JSON بحروف camelCase، والترميز UTF-8، و`contentType = application/json`.

```json
{
  "messageId": "6f1c2c1e-3a47-4f0e-9d63-0b8f1d2e7a10",
  "orderId": "b3a1e9c4-52d7-4b7e-8c1a-2f6d9a41c7e5",
  "userId": "9d2f7a58-1c3e-4a66-b0f4-5e8d3c2a1b97",
  "customerName": "Khadija",
  "customerEmail": "customer@example.com",
  "total": 150.00,
  "items": [
    {
      "productId": "4c7e1a02-8b15-4d3f-a9e6-7f2b5c8d0e31",
      "productName": "Keyboard",
      "unitPrice": 50.00,
      "quantity": 2
    },
    {
      "productId": "e8a3d6b1-2f49-4c70-95a8-1d7e0b3c6f52",
      "productName": "Mouse",
      "unitPrice": 50.00,
      "quantity": 1
    }
  ],
  "createdAtUtc": "2026-11-14T10:00:00Z"
}
```

| الحقل | المصدر في الكود |
|---|---|
| `messageId` | `Guid.NewGuid()` عند النشر (للتتبع في الـ logs) |
| `orderId`, `userId` | `Order.Id`, `Order.UserId` |
| `customerName`, `customerEmail` | `ICustomerContact.GetCurrent()` (من claims الـ JWT: `ClaimTypes.Name` و`ClaimTypes.Email`) |
| `total` | `Order.Total` |
| `items[]` | `OrderItem`: `ProductId`, `ProductName`, `UnitPrice`, `Quantity` |
| `createdAtUtc` | `Order.CreatedAt` |

**حالات خاصة:**

- إن لم يتوفر بريد الزبون (`GetCurrent()` ترجع `null`): يُسجَّل تحذير ولا تُنشر الرسالة، ولا يفشل الطلب.
- إن كان RabbitMQ متوقفاً لحظة الطلب: يُحفظ الطلب ويُسجَّل الخطأ ولا يفشل الطلب. هذا القرار المقترح، ويُراجَع في S0 إذا تغيّر.

---

## 4. WebSocket (بروتوكول Pusher)

الخادم جاهز (غير مكتوب من الصفر) ويعمل في حاوية. **الخادم المقترح: Soketi**، ويُحسم نهائياً في المهمة S0-3.

### القنوات والأحداث

| القناة | من يشترك | الحدث | المحتوى |
|---|---|---|---|
| `private-admin-orders` | المسؤولون فقط (`Admin`) | `order.created` | `OrderDto` كاملاً (انظر أدناه) |
| `private-order.{orderId}` | صاحب الطلب فقط | `order.status-changed` | انظر أدناه |

**محتوى `order.created` على القناة** هو نفس `OrderDto` الذي يرجعه الـ REST API، ليعيد الـ frontend استعمال شكله:

```json
{
  "id": "b3a1e9c4-52d7-4b7e-8c1a-2f6d9a41c7e5",
  "userId": "9d2f7a58-1c3e-4a66-b0f4-5e8d3c2a1b97",
  "status": "Pending",
  "total": 150.00,
  "createdAt": "2026-11-14T10:00:00Z",
  "items": [
    { "productId": "4c7e1a02-8b15-4d3f-a9e6-7f2b5c8d0e31", "productName": "Keyboard", "unitPrice": 50.00, "quantity": 2 },
    { "productId": "e8a3d6b1-2f49-4c70-95a8-1d7e0b3c6f52", "productName": "Mouse", "unitPrice": 50.00, "quantity": 1 }
  ]
}
```

**محتوى `order.status-changed`:**

```json
{
  "orderId": "b3a1e9c4-52d7-4b7e-8c1a-2f6d9a41c7e5",
  "status": "Confirmed",
  "occurredAtUtc": "2026-11-14T10:05:00Z"
}
```

قيم `status` نصية: `Pending` · `Confirmed` · `Shipped` · `Delivered` · `Cancelled` (من `OrderStatus`). يجب ترميز الـ enum كنص عند النشر والبث. **موافقة المسؤول تقابل `Confirmed`.**

### مصادقة القنوات

- المسار: `POST /api/v1/realtime/auth` ويتطلب `[Authorize]` (JWT في header `Authorization: Bearer ...`).
- الطلب (form-urlencoded كما يرسله `pusher-js`): `socket_id` و`channel_name`.
- الرد عند النجاح: `200` مع JSON فيه حقل `auth` (التوقيع الذي يولّده Pusher SDK).
- قواعد الرفض (`403`):
  - `private-admin-orders`: يُسمح فقط لدور `Admin`.
  - `private-order.{orderId}`: يُسمح فقط إن كان `Order.UserId` يساوي المستخدم الحالي (التحقق عبر `IOrdersApi.GetAsync`، والطلب غير الموجود يُرفض).
  - أي اسم قناة آخر: `403`.
- **قناة الطلب تتحقق من الملكية، وقناة المسؤول تتحقق من الدور.**

### العناوين

| من أين | العنوان |
|---|---|
| التطبيق (يبثّ الأحداث) | `PUSHER_HOST:PUSHER_PORT` = `localhost:6001` |
| المتصفح (`pusher-js`) | `VITE_PUSHER_HOST:VITE_PUSHER_PORT` = `localhost:6001` |
| داخل شبكة Docker (إن عمل التطبيق في حاوية لاحقاً) | اسم خدمة خادم WebSocket في compose، وليس `localhost` |

---

## 5. نقاط الدمج في كود AppService

| الحدث | الموضع |
|---|---|
| طلب جديد (`order.created` في الطابور وعلى القناة) | `OrdersService.CreateCoreAsync`، بعد نجاح `SaveChangesAsync` مباشرة (يغطي `POST /orders` و`POST /orders/checkout`) |
| تغيّر الحالة (`order.status-changed`) | `OrdersService.UpdateStatusAsync`، بعد نجاح التحديث (`PATCH /orders/{id}/status`، دور `Admin`) |

نمط الإضافة هو نمط Ports وAdapters الحالي:

- ports جديدة في `Orders.Module/Ports.cs`: `ICustomerContact` و`IOrderEventPublisher` (أ)، و`IOrderRealtimeNotifier` (ج).
- adapters حقيقية في `Orders.Module/Adapters`، وبدائل وهمية في `Orders.Module/Stubs` (للعمل بدون Docker)، والتسجيل في `OrdersModule.AddOrdersModule`.
- `IOrdersApi` العامة لا تتغير.

---

## 6. متغيرات البيئة (`.env`)

| المتغير | المالك | ملاحظة |
|---|---|---|
| `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | أ | مستخدم مخصص، لا `guest` |
| `RABBITMQ_HOST`, `RABBITMQ_PORT` | أ | `localhost` للتطبيق، و`rabbitmq` للـ Worker داخل compose |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | ب | بيانات حقيقية لمزوّد البريد، **لا تُرفع إلى المستودع** |
| `PUSHER_APP_ID`, `PUSHER_APP_KEY`, `PUSHER_APP_SECRET` | ج | تُشارَك مع خدمة خادم WebSocket |
| `PUSHER_HOST`, `PUSHER_PORT` | ج | لاتصال التطبيق بالخادم |
| `VITE_PUSHER_KEY`, `VITE_PUSHER_HOST`, `VITE_PUSHER_PORT` | د | لاتصال المتصفح، ولا يوضع فيها السر `SECRET` |

القواعد:

- `.env` داخل `.gitignore` ولا يُرفع. الملف المشترك هو `.env.example` بقيم وهمية.
- أي متغير جديد يُضاف إلى `.env.example` في الـ PR نفسه.

---

## 7. قرارات مفتوحة (تُحسم في S0)

| القرار | المسؤول | الحالة |
|---|---|---|
| مزوّد البريد (يصل لصندوق حقيقي) | ب (S0-2) | مفتوح |
| خادم WebSocket (Soketi أو Reverb أو غيره) | ج (S0-3) | مفتوح، المقترح Soketi |
| سلوك الطلب عند توقف RabbitMQ | الفريق | مقترح: يُحفظ الطلب ويُسجَّل الخطأ |

---

## 8. تعريف الإنجاز (Definition of Done)

- المهمة تصير «تم» فقط بعد **دمج الـ PR وتجربتها فعلياً**.
- الاختبار النهائي : إيقاف الـ Worker وإنشاء عدة طلبات ثم تشغيله فتصل كل الرسائل، ومتصفحان يتبادلان التحديثات فوراً بلا تحديث للصفحة.