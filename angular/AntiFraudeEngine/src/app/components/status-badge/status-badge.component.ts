import { Component, input } from '@angular/core';

const STATUS_COLORS: Record<string, string> = {
	RECEIVED: 'bg-blue-100 text-blue-800',
	PROCESSING: 'bg-yellow-100 text-yellow-800',
	APPROVED: 'bg-green-100 text-green-800',
	REJECTED: 'bg-red-100 text-red-800',
	REVIEW: 'bg-orange-100 text-orange-800'
};

@Component({
	selector: 'app-status-badge',
	standalone: true,
	template: `<span [class]="badgeClass()">{{ status() }}</span>`
})
export class StatusBadgeComponent {
	status = input.required<string>();

	badgeClass(): string {
		const base = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold tracking-wide uppercase';
		const color = STATUS_COLORS[this.status()] ?? 'bg-gray-100 text-gray-800';
		return `${base} ${color}`;
	}
}
