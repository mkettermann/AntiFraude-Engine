export interface AuditLogEntry {
	fromStatus: string;
	toStatus: string;
	occurredAt: string;
	message: string;
}

export interface TransactionRequest {
	transactionId: string;
	amount: number;
	merchantId: string;
	customerId: string;
	currency: string;
	metadata?: Record<string, string>;
}

export interface TransactionResponse {
	transactionId: string;
	status: string;
	decision: string | null;
	reason: string | null;
	createdAt: string;
	processedAt: string | null;
	auditTrail: AuditLogEntry[];
}

export interface SubmitResult {
	response: TransactionResponse;
	isIdempotentReplay: boolean;
	statusCode: number;
}
