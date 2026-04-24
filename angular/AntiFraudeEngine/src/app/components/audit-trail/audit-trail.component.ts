import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuditLogEntry } from '../../models/transaction.model';

@Component({
	selector: 'app-audit-trail',
	standalone: true,
	imports: [DatePipe],
	templateUrl: './audit-trail.component.html'
})
export class AuditTrailComponent {
	entries = input.required<AuditLogEntry[]>();
}
