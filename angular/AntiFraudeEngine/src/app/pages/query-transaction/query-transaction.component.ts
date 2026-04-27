import { Component, inject, signal, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { TransactionService } from '../../services/transaction.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { AuditTrailComponent } from '../../components/audit-trail/audit-trail.component';
import { TransactionResponse } from '../../models/transaction.model';

@Component({
	selector: 'app-query-transaction',
	standalone: true,
	imports: [ReactiveFormsModule, DatePipe, RouterLink, StatusBadgeComponent, AuditTrailComponent],
	templateUrl: './query-transaction.component.html'
})
export class QueryTransactionComponent implements OnInit {
	private readonly service = inject(TransactionService);
	private readonly route = inject(ActivatedRoute);

	searchControl = new FormControl('');
	isLoading = signal(false);
	transaction = signal<TransactionResponse | null>(null);
	errorMessage = signal<string | null>(null);
	searched = signal(false);

	ngOnInit(): void {
		const id = this.route.snapshot.queryParamMap.get('id');
		if (id) {
			this.searchControl.setValue(id);
			this.search();
		}
	}

	search(): void {
		const id = this.searchControl.value?.trim();
		if (!id) return;

		this.isLoading.set(true);
		this.errorMessage.set(null);
		this.transaction.set(null);
		this.searched.set(true);

		this.service.getTransaction(id).subscribe({
			next: (res) => {
				this.transaction.set(res);
				this.isLoading.set(false);
			},
			error: (err) => {
				this.isLoading.set(false);
				if (err.status === 404) {
					this.errorMessage.set('Transação não encontrada.');
				} else {
					this.errorMessage.set('Erro ao consultar transação. Verifique se a API está acessível.');
				}
			}
		});
	}
}
