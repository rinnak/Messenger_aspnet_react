import {AccountProfile} from "../../types";
import AccountForm from "../../features/AccountForm"
import styles from "./LoginPage.module.css";
import AuthStage from "../../features/AuthStage";

type Props = {
    onLogin: (profile: AccountProfile) => void;
}

export default function LoginPage({onLogin}: Props){
return (
    <AuthStage>
        <div className={styles.page}>
            <AccountForm mode="login" onSuccess={onLogin} />
        </div>
    </AuthStage>
);
}