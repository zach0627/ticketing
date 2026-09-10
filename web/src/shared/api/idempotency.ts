/** 每個新操作一個 key；重送時**不能**換。 */
export const newIdempotencyKey = (): string => crypto.randomUUID();
