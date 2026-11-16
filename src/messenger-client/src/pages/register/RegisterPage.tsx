import AuthStage from "../../features/AuthStage";
import styles from "./RegisterPage.module.css";
import AccountForm from "../../features/AccountForm";
import {useNavigate} from "react-router-dom";
export default function RegisterPage(){
    const navigate = useNavigate();
    return (
        <AuthStage>
            <div className={styles.page}>
                <AccountForm mode="register" onSuccess={() => navigate("/login", {replace: true})} />
            </div>
        </AuthStage>
    )
}
