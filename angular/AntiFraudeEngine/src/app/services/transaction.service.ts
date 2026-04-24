import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { TransactionRequest, TransactionResponse, SubmitResult } from '../models/transaction.model';

@Injectable({ providedIn: 'root' })
export class TransactionService {
	private readonly http = inject(HttpClient);
	private readonly baseUrl = environment.apiUrl;

	submitTransaction(request: TransactionRequest, idempotencyKey: string): Observable<SubmitResult> {
		const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
		return this.http.post<TransactionResponse>(
			`${this.baseUrl}/transactions`,
			request,
			{ headers, observe: 'response' }
		).pipe(
			map((res: HttpResponse<TransactionResponse>) => ({
				response: res.body!,
				isIdempotentReplay: res.status === 200,
				statusCode: res.status
			}))
		);
	}

	getTransaction(transactionId: string): Observable<TransactionResponse> {
		return this.http.get<TransactionResponse>(`${this.baseUrl}/transactions/${transactionId}`);
	}
}
