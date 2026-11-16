import {AccountProfile} from "../types";
import styles from "./AccountForm.module.css";
import {FormEvent, useState} from "react";
import {api, getErrorMessage} from "../api";
import {Link} from "react-router-dom";

type Props = {
    mode: "login" | "register";
    onSuccess: (profile: AccountProfile) => void;
}

export default function AccountForm({ mode, onSuccess }: Props){
    const register = mode === "register";
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);

    async function submit(event: FormEvent<HTMLFormElement>){
        event.preventDefault();
        const form = new FormData(event.currentTarget);

        const credentials = {
            username: String(form.get("username") ?? ""),
            password: String(form.get("password") ?? ""),
        };

        const body = register
        ? {...credentials, displayName: String(form.get("displayName") ?? "")}
            : credentials;

        setError("");
        setBusy(true);

        try{
            const profile = await api<AccountProfile>(
                register ? "/api/auth/register" : "/api/auth/login",
                {method: "POST", body: body}
            );
            onSuccess(profile);
        } catch (error) {
            setError(getErrorMessage(error));
        } finally {
            setBusy(false);
        }

    }
    return (
        <section className={styles.auth}>
            <h1>{register ? "Регистрация" : "Вход"}</h1>
            <form className={styles.form} onSubmit={submit}>
                <label>
                    Логин
                    <input name="username" type="text" autoComplete="username" required maxLength={24}/>
                </label>

                {register && (
                    <label>
                        Отображаемое имя
                        <input name="displayName" type="text" required maxLength={50}/>
                    </label>
                )}

                <label>
                    Пароль
                    <input
                        name="password"
                        type="password"
                        autoComplete={register ? "new-password" : "current-password"}
                        required
                        maxLength={128}
                    />
                </label>

                {error && <p className={styles.error} role="alert">{error}</p>}
                <button type="submit" className={styles.primary} disabled={busy}>
                    {busy ? "Отправляем..." : register ? "Создать аккаунт" : "Войти"}
                </button>
            </form>

            <Link className={styles.link} to={register ? "/login" : "/register"}>
                {register ? "У меня есть аккаунт" : "Создать аккаунт"}
            </Link>
        </section>
    )
}