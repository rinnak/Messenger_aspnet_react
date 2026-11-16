import type {ReactNode} from "react";
import styles from "./AuthStage.module.css";

type Props = {children: ReactNode};

export default function AuthStage({children}: Props) {
    return (
        <div className={styles.stage}>
            <div className={styles.content}>{children}</div>
        </div>
    )
}