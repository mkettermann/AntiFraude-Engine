import { Routes } from '@angular/router';

export const routes: Routes = [
	{
		path: '',
		loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent)
	},
	{
		path: 'submit',
		loadComponent: () => import('./pages/submit-transaction/submit-transaction.component').then(m => m.SubmitTransactionComponent)
	},
	{
		path: 'query',
		loadComponent: () => import('./pages/query-transaction/query-transaction.component').then(m => m.QueryTransactionComponent)
	},
	{
		path: '**',
		redirectTo: ''
	}
];
