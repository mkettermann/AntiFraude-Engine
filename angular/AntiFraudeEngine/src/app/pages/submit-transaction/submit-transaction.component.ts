import { Component, inject, signal } from '@angular/core';
import {
	FormBuilder,
	FormGroup,
	FormArray,
	Validators,
	ReactiveFormsModule
} from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TransactionService } from '../../services/transaction.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { TransactionResponse } from '../../models/transaction.model';

@Component({
	selector: 'app-submit-transaction',
	standalone: true,
	imports: [ReactiveFormsModule, DatePipe, RouterLink, StatusBadgeComponent],
	templateUrl: './submit-transaction.component.html'
})
export class SubmitTransactionComponent {
	private readonly fb = inject(FormBuilder);
	private readonly service = inject(TransactionService);

	form: FormGroup = this.fb.group({
		transactionId: [crypto.randomUUID(), Validators.required],
		amount: [null, [Validators.required, Validators.min(0.01)]],
		merchantId: ['', Validators.required],
		customerId: ['', Validators.required],
		currency: ['BRL', Validators.required],
		metadata: this.fb.array([])
	});

	idempotencyKey = signal(crypto.randomUUID());
	isLoading = signal(false);
	result = signal<TransactionResponse | null>(null);
	isIdempotentReplay = signal(false);
	errorMessage = signal<string | null>(null);
	validationErrors = signal<string[]>([]);

	get metadataArray(): FormArray {
		return this.form.get('metadata') as FormArray;
	}

	addMetadata(): void {
		this.metadataArray.push(this.fb.group({ key: [''], value: [''] }));
	}

	removeMetadata(index: number): void {
		this.metadataArray.removeAt(index);
	}

	regenerateIds(): void {
		this.form.patchValue({ transactionId: crypto.randomUUID() });
		this.idempotencyKey.set(crypto.randomUUID());
		this.result.set(null);
		this.errorMessage.set(null);
		this.validationErrors.set([]);
	}

	submit(): void {
		if (this.form.invalid) {
			this.form.markAllAsTouched();
			return;
		}

		const raw = this.form.value;
		const metadata: Record<string, string> = {};
		(raw.metadata as { key: string; value: string }[]).forEach(m => {
			if (m.key?.trim()) metadata[m.key.trim()] = m.value ?? '';
		});

		const request = {
			transactionId: raw.transactionId,
			amount: Number(raw.amount),
			merchantId: raw.merchantId,
			customerId: raw.customerId,
			currency: raw.currency,
			...(Object.keys(metadata).length ? { metadata } : {})
		};

		this.isLoading.set(true);
		this.errorMessage.set(null);
		this.validationErrors.set([]);

		this.service.submitTransaction(request, this.idempotencyKey()).subscribe({
			next: (res) => {
				this.result.set(res.response);
				this.isIdempotentReplay.set(res.isIdempotentReplay);
				this.isLoading.set(false);
			},
			error: (err) => {
				this.isLoading.set(false);
				if (err.status === 422) {
					this.validationErrors.set(err.error?.errors ?? ['Dados inválidos.']);
				} else {
					this.errorMessage.set(err.error?.message ?? 'Erro ao submeter transação. Verifique se a API está acessível.');
				}
			}
		});
	}
}
