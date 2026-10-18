import { Component } from '@angular/core';
import { ShiftsPage } from './shifts/shifts-page';

@Component({
  selector: 'app-root',
  imports: [ShiftsPage],
  template: '<app-shifts-page />',
})
export class App {}
