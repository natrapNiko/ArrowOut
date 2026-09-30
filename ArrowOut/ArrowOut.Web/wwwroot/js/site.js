// Stuff used on every page. Page-specific scripts live with their pages.
(function () {
    'use strict';

    // Success messages close by themselves after a few seconds (errors stay until you close them).
    document.querySelectorAll('.alert-success.alert-dismissible').forEach(alert => {
        window.setTimeout(() => {
            if (window.bootstrap && document.body.contains(alert)) {
                window.bootstrap.Alert.getOrCreateInstance(alert).close();
            }
        }, 4000);
    });

    // ---------- Dark / light mode ----------
    // Both colour sets are already on the page (ThemeStyles), so switching is instant. The form post
    // just remembers the choice. Until the player picks one, the device setting decides.
    const rootElement = document.documentElement;
    const systemDark = window.matchMedia('(prefers-color-scheme: dark)');

    function currentMode() {
        return rootElement.dataset.mode || (systemDark.matches ? 'dark' : 'light');
    }

    function applyMode(mode) {
        rootElement.dataset.mode = mode;
        rootElement.setAttribute('data-bs-theme', mode);
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) {
            meta.setAttribute('content', getComputedStyle(document.body).backgroundColor);
        }
    }

    if (!rootElement.dataset.mode) {
        // Bootstrap's own components (modals, close buttons) follow the device setting too.
        rootElement.setAttribute('data-bs-theme', currentMode());
        systemDark.addEventListener('change', () => {
            if (!rootElement.dataset.mode) {
                rootElement.setAttribute('data-bs-theme', currentMode());
            }
        });
    }

    document.querySelectorAll('form[data-mode-toggle]').forEach(form => {
        form.addEventListener('submit', event => {
            event.preventDefault();
            const next = currentMode() === 'dark' ? 'light' : 'dark';
            applyMode(next);
            form.elements.mode.value = next;
            fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch' }
            }).catch(() => { /* offline: the switch still applies to this page */ });
        });
    });

    // ---------- Password fields ----------
    document.querySelectorAll('[data-password-toggle]').forEach(button => {
        const input = button.parentElement.querySelector('input');
        const icon = button.querySelector('i');
        button.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            icon.className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
            button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            input.focus();
        });
    });

    // Live checklist under a new password (same rules as the Identity password options in Program.cs).
    const passwordRules = {
        length: value => value.length >= 8,
        upper: value => /[A-Z]/.test(value),
        lower: value => /[a-z]/.test(value),
        digit: value => /\d/.test(value)
    };

    document.querySelectorAll('[data-password-rules]').forEach(list => {
        const input = document.querySelector(list.dataset.passwordRules);
        if (!input) {
            return;
        }
        const update = () => {
            list.querySelectorAll('[data-rule]').forEach(item => {
                const test = passwordRules[item.dataset.rule];
                item.classList.toggle('is-met', Boolean(test && test(input.value)));
            });
        };
        input.addEventListener('input', update);
        update();
    });

    // Forms that take a moment (like making a new board): show a spinner and stop double clicks.
    document.querySelectorAll('form[data-busy-text]').forEach(form => {
        form.addEventListener('submit', () => {
            const button = form.querySelector('[type="submit"]');
            if (!button) {
                return;
            }
            const spinner = document.createElement('span');
            spinner.className = 'spinner-border spinner-border-sm me-2';
            spinner.setAttribute('aria-hidden', 'true');
            button.replaceChildren(spinner, document.createTextNode(form.dataset.busyText));
            // Disable it after the submit goes through, otherwise the button's value doesn't get sent.
            window.setTimeout(() => { button.disabled = true; }, 0);
        });
    });
}());
