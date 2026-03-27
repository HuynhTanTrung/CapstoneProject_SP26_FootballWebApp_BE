# Huong Dan FE Tich Hop Subscription

Tai lieu nay chi mo ta cac API lien quan den subscription/payment ma backend vua bo sung.
Khong bao gom huong dan webhook SePay.

## 1. Tong quan nhanh

Base API:

```text
{BASE_URL}/api/subscriptions
```

Tat ca response REST deu theo wrapper chung:

```json
{
  "success": true,
  "message": "string",
  "data": {},
  "errors": []
}
```

Auth:

- `GET /plans` la public.
- Cac API con lai can `Authorization: Bearer {accessToken}`.

Trang thai quan trong:

- Subscription status: `Inactive`, `Active`, `Expired`
- Payment status: `Pending`, `Paid`

## 2. Cac API FE can dung

### 2.1 Lay danh sach goi subscription

```http
GET /api/subscriptions/plans
```

Muc dich:

- Render danh sach goi cho nguoi dung chon.
- FE nen lay dong tu API, khong hard-code plan code.

Response data:

```json
[
  {
    "code": "MONTHLY",
    "name": "Monthly Subscription",
    "description": "30-day premium subscription.",
    "price": 99000,
    "durationDays": 30
  }
]
```

### 2.2 Lay subscription hien tai cua user

```http
GET /api/subscriptions/me
Authorization: Bearer {token}
```

Muc dich:

- Hien trang thai hien tai cua tai khoan.
- Sau khi thanh toan thanh cong, goi lai API nay de refresh UI.

Response data:

```json
{
  "status": "Active",
  "isActive": true,
  "planCode": "MONTHLY",
  "planName": "Monthly Subscription",
  "startedAt": "2026-03-27T10:00:00Z",
  "expiresAt": "2026-04-26T10:00:00Z",
  "lastPaymentAt": "2026-03-27T10:00:00Z"
}
```

Neu user chua co goi, backend van tra `success = true`, nhung `data` se co dang:

```json
{
  "status": "Inactive",
  "isActive": false,
  "planCode": null,
  "planName": null,
  "startedAt": null,
  "expiresAt": null,
  "lastPaymentAt": null
}
```

### 2.3 Tao payment cho goi subscription

```http
POST /api/subscriptions/payments
Authorization: Bearer {token}
Content-Type: application/json
```

Body:

```json
{
  "planCode": "MONTHLY"
}
```

Muc dich:

- Tao 1 payment moi cho goi ma user chon.
- Backend tra ve QR + thong tin chuyen khoan + `paymentCode`.

Response data:

```json
{
  "paymentId": "7d2b97ef-9d14-4bb4-b9af-0f888f9af001",
  "paymentCode": "SUB260327102501ABC123",
  "planCode": "MONTHLY",
  "planName": "Monthly Subscription",
  "amount": 99000,
  "provider": "SePay",
  "status": "Pending",
  "bankCode": "TPBank",
  "accountNumber": "03154896703",
  "accountName": "Vo Thanh Dat",
  "transferContent": "SUB260327102501ABC123",
  "qrUrl": "https://qr.sepay.vn/img?...",
  "expiresAt": "2026-03-27T10:55:00Z",
  "createdAt": "2026-03-27T10:25:00Z",
  "paidAt": null,
  "sePayTransactionId": null,
  "sePayReferenceCode": null
}
```

FE nen luu lai it nhat:

- `paymentCode`
- `status`
- `qrUrl`
- `bankCode`
- `accountNumber`
- `accountName`
- `transferContent`
- `amount`
- `expiresAt`

### 2.4 Lay lai chi tiet payment theo `paymentCode`

```http
GET /api/subscriptions/payments/{paymentCode}
Authorization: Bearer {token}
```

Muc dich:

- Polling de kiem tra payment neu FE khong dung SSE.
- Xac nhan lai sau khi SSE bao thanh cong.

Response data giong API tao payment.

### 2.5 Lang nghe SSE de nhan thanh toan thanh cong

```http
GET /api/subscriptions/payments/{paymentCode}/events
Authorization: Bearer {token}
Accept: text/event-stream
```

Muc dich:

- Khi backend nhan webhook thanh cong va cap nhat DB xong, FE se duoc day event ngay.

Event co the nhan:

- `payment.snapshot`: gui ngay khi vua connect SSE
- `payment.succeeded`: gui khi payment da thanh cong

Du lieu event:

```json
{
  "event": "payment.succeeded",
  "message": "Subscription payment processed successfully.",
  "occurredAt": "2026-03-27T10:28:30Z",
  "payment": {
    "paymentId": "7d2b97ef-9d14-4bb4-b9af-0f888f9af001",
    "paymentCode": "SUB260327102501ABC123",
    "planCode": "MONTHLY",
    "planName": "Monthly Subscription",
    "amount": 99000,
    "provider": "SePay",
    "status": "Paid",
    "bankCode": "VPBank",
    "accountNumber": "0764536859",
    "accountName": "Huynh Minh Phuc",
    "transferContent": "SUB260327102501ABC123",
    "qrUrl": "https://qr.sepay.vn/img?...",
    "expiresAt": "2026-03-27T10:55:00Z",
    "createdAt": "2026-03-27T10:25:00Z",
    "paidAt": "2026-03-27T10:28:30Z",
    "sePayTransactionId": 123456789,
    "sePayReferenceCode": "MBVCB.123456789"
  }
}
```

Luu y:

- SSE co keep-alive dang comment, FE co the bo qua.
- Neu payment da `Paid` truoc khi FE connect, backend se gui ngay `payment.succeeded` roi dong stream.

## 3. Luong FE de xuat theo tung buoc

## Buoc 1: Mo man hinh subscription

FE goi:

1. `GET /api/subscriptions/plans`
2. `GET /api/subscriptions/me`

UI nen hien:

- Danh sach cac goi
- Trang thai subscription hien tai
- Neu user dang `Active`, hien ngay het han

## Buoc 2: User chon 1 goi

Khi user bam "Mua goi", FE goi:

```http
POST /api/subscriptions/payments
```

Body:

```json
{
  "planCode": "MONTHLY"
}
```

Neu thanh cong, hien:

- QR code tu `qrUrl`
- So tien tu `amount`
- Ngan hang tu `bankCode`
- So tai khoan tu `accountNumber`
- Ten tai khoan tu `accountName`
- Noi dung chuyen khoan tu `transferContent`
- Dem nguoc theo `expiresAt`

## Buoc 3: Bat dau lang nghe trang thai thanh toan

Ngay sau khi tao payment thanh cong:

1. Luu `paymentCode`
2. Mo SSE voi `GET /api/subscriptions/payments/{paymentCode}/events`

Khuyen nghi:

- Neu frontend dung JWT Bearer trong header, khong nen dung `EventSource` native.
- Nen dung `@microsoft/fetch-event-source` hoac thu vien SSE cho phep set header.

Vi du:

```ts
import { fetchEventSource } from "@microsoft/fetch-event-source";

await fetchEventSource(
  `${BASE_URL}/api/subscriptions/payments/${paymentCode}/events`,
  {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`
    },
    onmessage(event) {
      if (!event.data) return;

      const payload = JSON.parse(event.data);

      if (event.event === "payment.snapshot") {
        setPayment(payload.payment);
      }

      if (event.event === "payment.succeeded") {
        setPayment(payload.payment);
        handlePaymentSuccess(payload.payment);
      }
    },
    onerror(error) {
      console.error("SSE error", error);
      throw error;
    }
  }
);
```

## Buoc 4: Xu ly khi thanh toan thanh cong

Trong `handlePaymentSuccess`, FE nen lam:

1. Dong SSE
2. Goi lai `GET /api/subscriptions/payments/{paymentCode}`
3. Goi lai `GET /api/subscriptions/me`
4. Refresh UI thanh cong

Ly do:

- `GET /payments/{paymentCode}` de dong bo trang thai payment moi nhat
- `GET /me` de dong bo subscription moi nhat cua user

## Buoc 5: Hien ket qua cho nguoi dung

Khi `payment.status === "Paid"`:

- Hien thong bao thanh cong
- An QR/payment instruction
- Hien subscription moi
- Hien `expiresAt` moi tu API `GET /api/subscriptions/me`

## 4. Fallback neu SSE bi loi

Neu SSE mat ket noi hoac FE khong muon dung SSE ngay:

1. Sau khi tao payment, bat polling `GET /api/subscriptions/payments/{paymentCode}`
2. Moi 5-10 giay goi 1 lan
3. Neu `status === "Paid"` thi dung polling
4. Goi `GET /api/subscriptions/me` de cap nhat subscription

Khuyen nghi:

- Uu tien SSE
- Polling la fallback

## 5. Thu tu goi API khuyen nghi

Flow day du:

1. `GET /api/subscriptions/plans`
2. `GET /api/subscriptions/me`
3. User chon goi
4. `POST /api/subscriptions/payments`
5. Hien QR + thong tin chuyen khoan
6. Mo SSE `GET /api/subscriptions/payments/{paymentCode}/events`
7. Nhan `payment.succeeded`
8. `GET /api/subscriptions/payments/{paymentCode}`
9. `GET /api/subscriptions/me`
10. Cap nhat UI thanh cong

## 6. Goi y state phia FE

FE nen co it nhat cac state sau:

```ts
type SubscriptionPageState = {
  plans: SubscriptionPlan[];
  currentSubscription: UserSubscription | null;
  selectedPlanCode: string | null;
  currentPayment: SubscriptionPayment | null;
  paymentCode: string | null;
  isCreatingPayment: boolean;
  isListeningPayment: boolean;
  paymentError: string | null;
};
```

## 7. TypeScript type goi y

```ts
export type ApiResponse<T> = {
  success: boolean;
  message: string;
  data: T;
  errors: string[];
};

export type SubscriptionPlan = {
  code: string;
  name: string;
  description?: string | null;
  price: number;
  durationDays: number;
};

export type UserSubscription = {
  status: "Inactive" | "Active" | "Expired";
  isActive: boolean;
  planCode?: string | null;
  planName?: string | null;
  startedAt?: string | null;
  expiresAt?: string | null;
  lastPaymentAt?: string | null;
};

export type SubscriptionPayment = {
  paymentId: string;
  paymentCode: string;
  planCode: string;
  planName: string;
  amount: number;
  provider: string;
  status: "Pending" | "Paid";
  bankCode: string;
  accountNumber: string;
  accountName: string;
  transferContent: string;
  qrUrl: string;
  expiresAt: string;
  createdAt: string;
  paidAt?: string | null;
  sePayTransactionId?: number | null;
  sePayReferenceCode?: string | null;
};

export type SubscriptionPaymentSseEvent = {
  event: "payment.snapshot" | "payment.succeeded";
  message: string;
  occurredAt: string;
  payment: SubscriptionPayment;
};
```

## 8. Luu y nho cho FE

- Khong hard-code plan code, hay lay tu `GET /plans`
- Luon luu `paymentCode` sau khi tao payment
- Luon refresh `GET /me` sau khi thanh toan thanh cong
- Dung `expiresAt` de dem nguoc thoi gian thanh toan
- Neu user refresh trang giua chung, FE co the luu `paymentCode` tam vao local state/storage va goi lai `GET /payments/{paymentCode}`

