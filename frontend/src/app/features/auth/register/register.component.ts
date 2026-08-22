import {
  Component,
  inject,
  ChangeDetectionStrategy,
  OnInit,
} from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
  ReactiveFormsModule,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthStore } from '../../../core/auth/auth.store';
import { RegisterRequest } from '../../../core/auth/models/register-request.model';

function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent implements OnInit {
  protected readonly authStore = inject(AuthStore);
  private readonly fb = inject(FormBuilder);

  protected form!: FormGroup;
  protected hidePassword = true;
  protected hideConfirm = true;

  ngOnInit(): void {
    this.form = this.fb.group(
      {
        firstName: ['', [Validators.required, Validators.minLength(2)]],
        lastName: ['', [Validators.required, Validators.minLength(2)]],
        email: ['', [Validators.required, Validators.email]],
        password: [
          '',
          [
            Validators.required,
            Validators.minLength(8),
            Validators.pattern(
              /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/
            ),
          ],
        ],
        confirmPassword: ['', Validators.required],
      },
      { validators: passwordMatchValidator }
    );

    this.form.valueChanges.subscribe(() => {
      if (this.authStore.error()) {
        this.authStore.clearError();
      }
    });
  }

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const request: RegisterRequest = {
      firstName: this.form.value.firstName.trim(),
      lastName: this.form.value.lastName.trim(),
      email: this.form.value.email.trim().toLowerCase(),
      password: this.form.value.password,
      confirmPassword: this.form.value.confirmPassword,
    };
    await this.authStore.register(request);
  }

  protected getFieldError(fieldName: string): string {
    const ctrl = this.form.get(fieldName);
    if (!ctrl?.touched) return '';
    switch (fieldName) {
      case 'firstName':
        if (ctrl.hasError('required')) return 'First name is required';
        if (ctrl.hasError('minlength')) return 'Must be at least 2 characters';
        break;
      case 'lastName':
        if (ctrl.hasError('required')) return 'Last name is required';
        if (ctrl.hasError('minlength')) return 'Must be at least 2 characters';
        break;
      case 'email':
        if (ctrl.hasError('required')) return 'Email is required';
        if (ctrl.hasError('email')) return 'Enter a valid email address';
        break;
      case 'password':
        if (ctrl.hasError('required')) return 'Password is required';
        if (ctrl.hasError('minlength')) return 'Password must be at least 8 characters';
        if (ctrl.hasError('pattern'))
          return 'Must contain uppercase, lowercase, number and special character';
        break;
      case 'confirmPassword':
        if (ctrl.hasError('required')) return 'Please confirm your password';
        if (this.form.hasError('passwordMismatch')) return 'Passwords do not match';
        break;
    }
    return '';
  }
}